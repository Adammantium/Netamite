using Steamworks;

namespace Netamite.Steam.Datatype {
    public struct Lobby {
        public CSteamID LobbyId;
        public CSteamID OwnerId;
        public LobbyMember[] Members;
        public int MemberLimit;
        public LobbyMetaData[] Data;
    }
}
