using Netamite.Client.Implementation;
using Netamite.Datatype;
using Netamite.Logging;
using Netamite.Network.Packet;
using Netamite.Server.Data;
using Netamite.Server.Definition;
using Netamite.Steam.Datatype;
using Netamite.Steam.Integration;
using Netamite.Steam.Socket;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Threading;
using SteamClient = Netamite.Steam.Client.SteamClient;

namespace Netamite.Steam.Server {
    public class SteamServer : NetamiteServer {
        private SteamServerCallbacks steamServerCallbacks;

        public Lobby currentLobby;

        private ELobbyType lobbyType = ELobbyType.k_ELobbyTypeFriendsOnly;


        private Dictionary<int, ulong> userIdToSteamMapping = new Dictionary<int, ulong>();
        private Dictionary<ulong, int> steamIdToUserMapping = new Dictionary<ulong, int>();
        private Dictionary<int, SteamSocket> steamUserReliableMapping = new Dictionary<int, SteamSocket>();
        private Dictionary<int, SteamSocket> steamUserUnreliableMapping = new Dictionary<int, SteamSocket>();

        private SteamSocket reliableReceiveSocket;
        private SteamSocket unreliableReceiveSocket;

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

            reliableReceiveSocket.Disconnect();
            unreliableReceiveSocket.Disconnect();

            foreach(SteamSocket steamSocket in steamUserReliableMapping.Values) {
                steamSocket.Disconnect();
            }
            foreach(SteamSocket steamSocket in steamUserUnreliableMapping.Values) {
                steamSocket.Disconnect();
            }

            steamServerCallbacks.UnregisterCallbacks();

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
                if(!userIdToSteamMapping.ContainsValue((ulong)member.SteamId)) {
                    ClientInformation clientInformation = new ClientInformation();
                    clientInformation.ClientName = member.Name;

                    clientInformation.ClientId = NextId;

                    userIdToSteamMapping.Add(clientInformation.ClientId, (ulong) member.SteamId);
                    steamIdToUserMapping.Add((ulong) member.SteamId, clientInformation.ClientId);

                    SteamSocket reliableSocket = new SteamSocket(member.SteamId, EP2PSend.k_EP2PSendReliable, SteamNetConfig.STEAM_SERVER_RELIABLE_CHANNEL);
                    reliableSocket.StartSending();
                    SteamSocket unreliableSocket = new SteamSocket(member.SteamId, EP2PSend.k_EP2PSendUnreliableNoDelay, SteamNetConfig.STEAM_SERVER_UNRELIABLE_CHANNEL);
                    unreliableSocket.StartSending(); 
                    
                    steamUserReliableMapping.Add(clientInformation.ClientId, reliableSocket);
                    steamUserUnreliableMapping.Add(clientInformation.ClientId, unreliableSocket);

                    ClientJoined(clientInformation);
                }
            }
            foreach(KeyValuePair<int, ulong> user in userIdToSteamMapping) {
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

            if(userIdToSteamMapping.ContainsKey(client.ClientId)) {
                if(steamIdToUserMapping.ContainsKey(userIdToSteamMapping[client.ClientId])) {
                    steamIdToUserMapping.Remove(userIdToSteamMapping[client.ClientId]);
                }
                userIdToSteamMapping.Remove(client.ClientId);
            }
        }

        internal void OnLobbyCreate(LobbyCreated_t req) {
            if(req.m_eResult == EResult.k_EResultOK) {
                SteamIntegration.GetLobbyInfo(req.m_ulSteamIDLobby, ref currentLobby);

                if(reliableReceiveSocket == null) {
                    reliableReceiveSocket = new SteamSocket(currentLobby.OwnerId, EP2PSend.k_EP2PSendReliable, SteamNetConfig.STEAM_SERVER_RELIABLE_CHANNEL);
                    reliableReceiveSocket.StartReceiving();
                    reliableReceiveSocket.onPacketWithId += OnPacketWithId;
                }
                if(unreliableReceiveSocket == null) {
                    unreliableReceiveSocket = new SteamSocket(currentLobby.OwnerId, EP2PSend.k_EP2PSendUnreliableNoDelay, SteamNetConfig.STEAM_SERVER_UNRELIABLE_CHANNEL);
                    unreliableReceiveSocket.StartReceiving();
                    unreliableReceiveSocket.onPacketWithId += OnPacketWithId;
                }

                base.InvokeStartEvent(true);
            } else {
                OnStartupError?.Invoke("Steam Lobby creation failed!");
            }
        }

        private void OnPacketWithId(ulong steamId, NetPacket p) {
            if(steamIdToUserMapping.ContainsKey(steamId)) {
                int userId = steamIdToUserMapping[steamId];
                ProcessPacket(_clients[userId], p);
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
            Send(client, true, p);
        }

        public override void SendUnreliableTo(ClientInformation client, NetPacket p) {
            Send(client, false, p);
        }

        private void Send(ClientInformation client, bool reliable, NetPacket packet) {
            if(userIdToSteamMapping.ContainsKey(client.ClientId)) {
                CSteamID target = (CSteamID) userIdToSteamMapping[client.ClientId];

                if(Netamite.MasterClient is SteamClient) {
                    SteamClient c = (SteamClient) Netamite.MasterClient;
                    if(SteamIntegration.SteamID == target) {
                        if(c.IsConnected) {
                            c.DataHandler(packet);
                        } else {
                            c.AwaitingConnectionHandler(packet);
                        }
                        return;
                    }
                }

                if(reliable) {
                    steamUserReliableMapping[client.ClientId].QueuePacket(packet);
                } else {
                    steamUserUnreliableMapping[client.ClientId].QueuePacket(packet);
                }
            }
        }
    }
}
