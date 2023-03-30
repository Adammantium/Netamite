using Netamite.Server.Definition;
using Netamite.Steam.Datatype;
using Netamite.Steam.Integration;
using Steamworks;
using System;

namespace Netamite.Steam.Server {
    public class SteamServer : NetamiteServer {
        private SteamServerCallbacks steamServerCallbacks;

        public Lobby currentLobby;

        private ELobbyType lobbyType = ELobbyType.k_ELobbyTypeFriendsOnly;

        public SteamServer(int maxClients, bool friendsOnly = true) : base(maxClients) {
            if(!friendsOnly) this.lobbyType = ELobbyType.k_ELobbyTypePublic;

            currentLobby = default(Lobby);
        }

        public override void Start() {
            base.InvokeStartEvent(false);
            base.Start();

            steamServerCallbacks = new SteamServerCallbacks(this);

            SteamMatchmaking.CreateLobby(lobbyType, MaxClients);
        }




        internal void OnLobbyChatUpdate(LobbyChatUpdate_t req) {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        internal void OnP2PSessionRequest(P2PSessionRequest_t req) {
            throw new NotImplementedException();
        }
    }
}
