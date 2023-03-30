using Steamworks;

namespace Netamite.Steam.Client {
    internal class SteamClientCallbacks {

        private bool registered = false;
        private SteamClient steamClient;

        // client connection state
        protected Callback<P2PSessionRequest_t> callbackP2PSessionRequest;
        protected Callback<P2PSessionConnectFail_t> callbackP2PSessionConnectFail;

        protected Callback<LobbyEnter_t> callbackLobbyEnter;
        protected Callback<LobbyChatUpdate_t> callbackLobbyChatUpdate;

        public SteamClientCallbacks(SteamClient steamClient) {
            this.steamClient = steamClient;
            RegisterCallbacks();
        }

        internal void RegisterCallbacks() {
            if(registered) return;

            callbackP2PSessionRequest     = Callback<P2PSessionRequest_t>         .Create(steamClient.OnP2PSessionRequest         );
            callbackP2PSessionConnectFail = Callback<P2PSessionConnectFail_t>     .Create(steamClient.OnP2PSessionConnectFail     );
            callbackLobbyEnter            = Callback<LobbyEnter_t>                .Create(steamClient.OnLobbyEnter                );
            callbackLobbyChatUpdate       = Callback<LobbyChatUpdate_t>           .Create(steamClient.OnLobbyChatUpdate           );

            registered = true;
        }

        internal void UnregisterCallbacks() {
            if(!registered) return;
            
            callbackP2PSessionRequest    .Dispose();
            callbackP2PSessionConnectFail.Dispose();
            callbackLobbyEnter           .Dispose();
            callbackLobbyChatUpdate      .Dispose();

            registered = false;
        }

    }
}
