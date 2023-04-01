using Netamite.Datatype;
using Netamite.Logging;
using Netamite.Network.Packet;
using Netamite.Network.Packet.Implementations;
using Netamite.Network.Socket;
using Netamite.Server.Data;
using Netamite.Server.Definition;
using Netamite.Steam.Client;
using Netamite.Steam.Datatype;
using Netamite.Steam.Integration;
using Netamite.Steam.Socket;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.Remoting.Channels;
using System.Threading;
using SteamClient = Netamite.Steam.Client.SteamClient;

namespace Netamite.Steam.Server {
    public class SteamServer : NetamiteServer {
        private SteamServerCallbacks steamServerCallbacks;

        public Lobby currentLobby;

        private ELobbyType lobbyType = ELobbyType.k_ELobbyTypeFriendsOnly;


        private Dictionary<int, ulong> steamUserIdMapping = new Dictionary<int, ulong>();
        private Dictionary<int, SteamSocket> steamUserReliableMapping = new Dictionary<int, SteamSocket>();
        private Dictionary<int, SteamSocket> steamUserUnreliableMapping = new Dictionary<int, SteamSocket>();

        public SteamServer(int maxClients, bool friendsOnly = true) : base() {
            MaxClients = maxClients;

            if(!friendsOnly) this.lobbyType = ELobbyType.k_ELobbyTypePublic;

            currentLobby = default(Lobby);
        }

        public override void Start() {
            base.InvokeStartEvent(false);
            base.Start();

            steamServerCallbacks = new SteamServerCallbacks(this);

            SteamMatchmaking.CreateLobby(lobbyType, MaxClients);
        }

        public override void Stop() {
            InvokeStopEvent(false); // TODO: Automatically done by the Steam Lobby

            foreach(SteamSocket steamSocket in steamUserReliableMapping.Values) {
                steamSocket.Disconnect();
            }
            foreach(SteamSocket steamSocket in steamUserUnreliableMapping.Values) {
                steamSocket.Disconnect();
            }

            InvokeStopEvent(true);
        }

        internal void OnLobbyChatUpdate(LobbyChatUpdate_t req) {
            if(req.m_ulSteamIDLobby == (ulong) currentLobby.LobbyId) {
                SteamIntegration.GetLobbyInfo(req.m_ulSteamIDLobby, ref currentLobby);

                CheckForNewMembers();
            }
        }

        internal void CheckForNewMembers() {
            foreach(LobbyMember member in currentLobby.Members) {
                if(!steamUserIdMapping.ContainsValue((ulong)member.SteamId)) {
                    ClientInformation clientInformation = new ClientInformation();
                    clientInformation.ClientName = member.Name;

                    clientInformation.ClientId = NextId;

                    steamUserIdMapping.Add(clientInformation.ClientId, (ulong)member.SteamId);

                    SteamSocket reliableSocket = new SteamSocket(member.SteamId, EP2PSend.k_EP2PSendReliable, clientInformation.ClientId);
                    reliableSocket.StopSending(); // Only needed for receiving, sending will be on channel defined in SteamNetConfig.STEAM_SERVER_RELIABLE_CHANNEL
                    reliableSocket.onPacketWithId = (id, p) => {
                        ProcessPacket(clientInformation, p);
                    };
                    SteamSocket unreliableSocket = new SteamSocket(member.SteamId, EP2PSend.k_EP2PSendUnreliableNoDelay, clientInformation.ClientId);
                    unreliableSocket.StopSending(); // Only needed for receiving, sending will be on channel defined in SteamNetConfig.STEAM_SERVER_UNRELIABLE_CHANNEL
                    unreliableSocket.onPacketWithId = (id, p) => {
                        ProcessPacket(clientInformation, p);
                    };

                    steamUserReliableMapping.Add(clientInformation.ClientId, reliableSocket);
                    steamUserUnreliableMapping.Add(clientInformation.ClientId, unreliableSocket);

                    ClientJoined(clientInformation);
                }
            }
            foreach(KeyValuePair<int, ulong> user in steamUserIdMapping) {
                bool found = false;
                foreach(LobbyMember member in currentLobby.Members) {
                    if(user.Value == (ulong) member.SteamId) {
                        found = true;
                        break;
                    }
                }
                if(!found) {
                    ClientLeft(Clients[user.Key]);
                }
            }
        }

        public override void ClientLeft(ClientInformation client, string reason = DisconnectReason.CONNECTION_CLOSED) {
            base.ClientLeft(client, reason);

            if(steamUserIdMapping.ContainsKey(client.ClientId)) {
                steamUserIdMapping.Remove(client.ClientId);
            }
        }

        internal void OnLobbyCreate(LobbyCreated_t req) {
            if(req.m_eResult == EResult.k_EResultOK) {
                SteamIntegration.GetLobbyInfo(req.m_ulSteamIDLobby, ref currentLobby);

                base.InvokeStartEvent(true);
            } else {
                OnStartupError?.Invoke("Steam Lobby creation failed!");
            }
        }

        internal void OnP2PSessionConnectFail(P2PSessionConnectFail_t req) {
            
        }

        internal void OnP2PSessionRequest(P2PSessionRequest_t req) {
            bool allow = false;
            foreach(LobbyMember member in currentLobby.Members) { // Check if the person trying to connect is a member of the lobby
                if(req.m_steamIDRemote == member.SteamId) {
                    allow = true;
                    break;
                }
            }

            if(allow) {
                SteamNetworking.AcceptP2PSessionWithUser(req.m_steamIDRemote);
            }
        }

        public override void SendReliableTo(ClientInformation client, NetPacket p) {
            Send(client, EP2PSend.k_EP2PSendReliable, SteamNetConfig.STEAM_SERVER_RELIABLE_CHANNEL, p);
        }

        public override void SendUnreliableTo(ClientInformation client, NetPacket p) {
            Send(client, EP2PSend.k_EP2PSendUnreliableNoDelay, SteamNetConfig.STEAM_SERVER_UNRELIABLE_CHANNEL, p);
        }

        private void Send(ClientInformation client, EP2PSend mode, int channel, NetPacket packet) {
            if(steamUserIdMapping.ContainsKey(client.ClientId)) {
                CSteamID target = (CSteamID)steamUserIdMapping[client.ClientId];

                if(Netamite.MasterClient is SteamClient) {
                    SteamClient c = (SteamClient) Netamite.MasterClient;
                    if(c.ClientId == client.ClientId || SteamIntegration.SteamID == target) {
                        if(c.IsConnected) {
                            c.DataHandler(packet);
                        } else {
                            c.AwaitingConnectionHandler(packet);
                        }
                        return;
                    }
                }

                byte[] data = packet.GetData(true);

                byte attempts = 5;
                if(mode == EP2PSend.k_EP2PSendUnreliable || mode == EP2PSend.k_EP2PSendUnreliableNoDelay) attempts = 0;

                while(!SteamNetworking.SendP2PPacket(target, data, (uint)data.Length, mode, channel)) {
                    attempts--;
                    if(attempts <= 0) break;
                    Thread.Sleep(0);
                }
            }
        }
    }
}
