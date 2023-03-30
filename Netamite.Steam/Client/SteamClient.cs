using Netamite.Client.Definition;
using Netamite.Logging;
using Netamite.Network.Packet;
using Netamite.Steam.Datatype;
using Netamite.Steam.Integration;
using Netamite.Steam.Socket;
using Steamworks;

namespace Netamite.Steam.Client {
    public class SteamClient : NetamiteClient {

        private ulong lobbyId = 0;
        private SteamClientCallbacks steamClientCallbacks;

        public Lobby currentLobby;

        private SteamSocket unreliableSocket;
        private SteamSocket reliableSocket;

        public SteamClient(ulong lobbyId) : base() {
            this.lobbyId = lobbyId;
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
        }

        public override void Disconnect() {
            reliableSocket?.Disconnect();
            reliableSocket = null;
            unreliableSocket?.Disconnect();
            unreliableSocket = null;

            LeaveLobby();

            steamClientCallbacks?.UnregisterCallbacks();

            currentLobby = default(Lobby);
            
            base.Disconnect();
        }

        public override void SendReliable(NetPacket packet) {
            if(reliableSocket != null) {
                reliableSocket.QueuePacket(packet);
                base.SendReliable(packet);
            }
        }

        public override void SendUnreliable(NetPacket packet) {
            if(unreliableSocket != null) {
                unreliableSocket.QueuePacket(packet);
                base.SendUnreliable(packet);
            }
        }


        internal void LeaveLobby() {
            if((ulong) currentLobby.LobbyId > 0) {
                SteamMatchmaking.LeaveLobby(currentLobby.LobbyId);
            }
        }

        internal void OnLobbyEnter(LobbyEnter_t req) {
            if(req.m_ulSteamIDLobby != (ulong) currentLobby.LobbyId) return;

            SteamIntegration.GetLobbyInfo(req.m_ulSteamIDLobby, ref currentLobby);
        }
        internal void OnLobbyChatUpdate(LobbyChatUpdate_t req) {
            if(req.m_ulSteamIDLobby != (ulong) currentLobby.LobbyId) return;

            SteamIntegration.GetLobbyInfo(req.m_ulSteamIDLobby, ref currentLobby);
        }

        internal void OnP2PSessionRequest(P2PSessionRequest_t req) {
            bool allow = false;
            if(req.m_steamIDRemote == currentLobby.OwnerId) { // Check if the person trying to connect is the host
                allow = true;

                reliableSocket   = new SteamSocket(currentLobby.OwnerId, EP2PSend.k_EP2PSendReliable,          SteamNetConfig.STEAM_SERVER_RELIABLE_CHANNEL  );
                unreliableSocket = new SteamSocket(currentLobby.OwnerId, EP2PSend.k_EP2PSendUnreliableNoDelay, SteamNetConfig.STEAM_SERVER_UNRELIABLE_CHANNEL);
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
