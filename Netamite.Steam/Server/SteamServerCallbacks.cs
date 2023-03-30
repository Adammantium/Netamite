using Steamworks;

namespace Netamite.Steam.Server {
    internal class SteamServerCallbacks {

        private bool registered = false;
        private SteamServer steamServer;

        // client connection state
        protected Callback<P2PSessionRequest_t> callbackP2PSessionRequest;
        protected Callback<P2PSessionConnectFail_t> callbackP2PSessionConnectFail;

        protected Callback<LobbyCreated_t> callbackLobbyCreate;
        protected Callback<LobbyChatUpdate_t> callbackLobbyChatUpdate;

        public SteamServerCallbacks(SteamServer steamServer) {
            this.steamServer = steamServer;
            RegisterCallbacks();
        }

        internal void RegisterCallbacks() {
            if(registered) return;

            callbackP2PSessionRequest     = Callback<P2PSessionRequest_t>    .Create(steamServer.OnP2PSessionRequest);
            callbackP2PSessionConnectFail = Callback<P2PSessionConnectFail_t>.Create(steamServer.OnP2PSessionConnectFail);
            callbackLobbyCreate           = Callback<LobbyCreated_t>         .Create(steamServer.OnLobbyCreate);
            callbackLobbyChatUpdate       = Callback<LobbyChatUpdate_t>      .Create(steamServer.OnLobbyChatUpdate);

            registered = true;
        }

        internal void UnregisterCallbacks() {
            if(!registered) return;

            callbackP2PSessionRequest    .Dispose();
            callbackP2PSessionConnectFail.Dispose();
            callbackLobbyCreate          .Dispose();
            callbackLobbyChatUpdate      .Dispose();

            registered = false;
        }
    }
}
