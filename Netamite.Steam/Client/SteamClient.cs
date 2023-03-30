using Netamite.Client.Definition;
using Netamite.Client.Implementation;
using Netamite.Logging;
using Netamite.Network.Packet;
using Netamite.Steam.Datatype;
using Netamite.Steam.Integration;
using Netamite.Steam.Server;
using Netamite.Steam.Socket;
using Steamworks;
using System.Net.Sockets;

namespace Netamite.Steam.Client {
    public class SteamClient : NetamiteClient {

        private ulong lobbyId = 0;
        private SteamClientCallbacks steamClientCallbacks;

        public Lobby currentLobby;

        private SteamSocket receiveReliableSocket;
        private SteamSocket receiveUnreliableSocket;
        private SteamSocket sendReliableSocket;
        private SteamSocket sendUnreliableSocket;

        public SteamClient(ulong lobbyId) : base() {
            this.lobbyId = lobbyId;

            ClientName = SteamIntegration.Username;
        }

        public override void Connect(string accessToken = "") {
            if(lobbyId <= 0) {
                STATE = "FAILED";
                Disconnect();
                return;
            }

            Log.Info(Defines.CLIENT, $"Connecting to lobby {lobbyId}...");

            currentLobby = default(Lobby);
            currentLobby.LobbyId = (CSteamID) lobbyId;

            steamClientCallbacks = new SteamClientCallbacks(this);

            lobbyEnteredEvent = 0;
            SteamMatchmaking.JoinLobby(currentLobby.LobbyId);
        }

        public override void Disconnect() {
            receiveReliableSocket?.Disconnect();
            receiveReliableSocket = null;
            receiveUnreliableSocket?.Disconnect();
            receiveUnreliableSocket = null;
            sendReliableSocket?.Disconnect();
            sendReliableSocket = null;
            sendUnreliableSocket?.Disconnect();
            sendUnreliableSocket = null;

            LeaveLobby();

            steamClientCallbacks?.UnregisterCallbacks();
            
            base.Disconnect();

            if(Netamite.MasterServer is SteamServer) {
                if(currentLobby.OwnerId == SteamIntegration.SteamID) {
                    SteamServer s = (SteamServer)Netamite.MasterServer;
                    s.Stop();
                }
            }

            currentLobby = default(Lobby);
        }

        public override void SendReliable(NetPacket packet) {
            if(Netamite.MasterServer is SteamServer) {
                if(currentLobby.OwnerId == SteamIntegration.SteamID) {
                    SteamServer s = (SteamServer) Netamite.MasterServer;
                    s.ProcessPacket(s._clients[ClientId], packet);
                    return;
                }
            }

            if(sendReliableSocket != null) {
                sendReliableSocket.QueuePacket(packet);
                base.SendReliable(packet);
            }
        }

        public override void SendUnreliable(NetPacket packet) {
            if(Netamite.MasterServer is SteamServer) {
                if(currentLobby.OwnerId == SteamIntegration.SteamID) {
                    SteamServer s = (SteamServer)Netamite.MasterServer;
                    s.ProcessPacket(s._clients[ClientId], packet);
                    return;
                }
            }

            if(sendReliableSocket != null) {
                sendReliableSocket.QueuePacket(packet);
                base.SendUnreliable(packet);
            }
        }


        internal void LeaveLobby() {
            if((ulong) currentLobby.LobbyId > 0) {
                SteamMatchmaking.LeaveLobby(currentLobby.LobbyId);
            }
        }

        private ulong lobbyEnteredEvent = 0; 
        internal void OnLobbyEnter(LobbyEnter_t req) {
            if(req.m_ulSteamIDLobby != (ulong) currentLobby.LobbyId) return;
            if(lobbyEnteredEvent == req.m_ulSteamIDLobby) return;

            SteamIntegration.GetLobbyInfo(req.m_ulSteamIDLobby, ref currentLobby);

            Log.Info(Defines.CLIENT, $"Lobby {lobbyId} entered, finializing connection...");

            OnDataReceived += AwaitingConnectionHandler;

            if(Netamite.MasterServer is SteamServer) {
                ((SteamServer) Netamite.MasterServer).CheckForNewMembers();
            }
            lobbyEnteredEvent = req.m_ulSteamIDLobby;
        }
        public override void OnConnect() {
            sendReliableSocket = new SteamSocket(currentLobby.OwnerId, EP2PSend.k_EP2PSendReliable, ClientId);
            sendReliableSocket.StopReceiving();
            sendUnreliableSocket = new SteamSocket(currentLobby.OwnerId, EP2PSend.k_EP2PSendUnreliableNoDelay, ClientId);
            sendUnreliableSocket.StartReceiving();

            Log.Info(Defines.CLIENT, $"Lobby {lobbyId} entered!");

            base.onConnect();
        }

        internal void OnLobbyChatUpdate(LobbyChatUpdate_t req) {
            if(req.m_ulSteamIDLobby != (ulong) currentLobby.LobbyId) return;

            SteamIntegration.GetLobbyInfo(req.m_ulSteamIDLobby, ref currentLobby);
        }

        internal void OnP2PSessionRequest(P2PSessionRequest_t req) {
            bool allow = false;
            if(req.m_steamIDRemote == currentLobby.OwnerId) { // Check if the person trying to connect is the host
                allow = true;

                receiveReliableSocket   = new SteamSocket(currentLobby.OwnerId, EP2PSend.k_EP2PSendReliable,          SteamNetConfig.STEAM_SERVER_RELIABLE_CHANNEL  );
                receiveReliableSocket.StopSending();
                receiveReliableSocket.onPacket += OnReliableReceived;
                receiveUnreliableSocket = new SteamSocket(currentLobby.OwnerId, EP2PSend.k_EP2PSendUnreliableNoDelay, SteamNetConfig.STEAM_SERVER_UNRELIABLE_CHANNEL);
                receiveUnreliableSocket.StopSending();
                receiveUnreliableSocket.onPacket += OnUnreliableReceived;
            }

            if(allow) {
                SteamNetworking.AcceptP2PSessionWithUser(req.m_steamIDRemote);
            } else {
                onWarning?.Invoke("Connection denied from unknown SteamId " + req.m_steamIDRemote);
            }
        }

        internal void OnP2PSessionConnectFail(P2PSessionConnectFail_t req) {
            if(req.m_steamIDRemote != currentLobby.OwnerId) return;

            onConnectionError?.Invoke("Steam P2P Session Connect failed");
            Disconnect();
        }
    }
}
