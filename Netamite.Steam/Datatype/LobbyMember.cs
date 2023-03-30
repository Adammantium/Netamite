using Steamworks;

namespace Netamite.Steam.Datatype {
    public struct LobbyMember {
        public CSteamID SteamId;
        public string Name;
        public LobbyMetaData[] Data;
    }
}
