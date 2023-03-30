using Netamite.Steam.Datatype;
using Netamite.Steam.Exception;
using Steamworks;
using System;

namespace Netamite.Steam.Integration {
    public class SteamIntegration {
        public static Action OnInitialized;
        public static Action<System.Exception> OnError;

        private static bool _isInitialized = false;
        public static bool IsInitialized { 
            get { return _isInitialized; }
        }

        private static string _Username = "";
        public static string Username { get { return _Username; } }

        private static CSteamID _SteamID;
        public static CSteamID SteamID { get { return _SteamID; } }

        public static bool Initialize(long AppId) {
            _isInitialized = false;
            _isInitialized = SteamAPI.Init();
            if(!_isInitialized) {
                OnError?.Invoke(new SteamNotDetectedException());
                return false;
            }

            try {
                if(SteamAPI.RestartAppIfNecessary((AppId_t) AppId)) {
                    OnError?.Invoke(new SteamNotDetectedException());
                    return false;
                }

                _Username = SteamFriends.GetPersonaName();
                _SteamID = SteamUser.GetSteamID();
            } catch(DllNotFoundException e) {
                OnError?.Invoke(e);
                return false;
            }

            RegisterCallbacks();
            
            _isInitialized = true;

            SteamNetworking.AllowP2PPacketRelay(true);

            OnInitialized?.Invoke();
            return true;
        }

        private static void RegisterCallbacks() {
            
        }

        public static void RunCallbacks() {
            if(!_isInitialized) return;
            SteamAPI.RunCallbacks();
        }

        public static Lobby GetLobbyInfo(ulong lobbyId) {
            Lobby lobby = default(Lobby);
            GetLobbyInfo(lobbyId, ref lobby);
            return lobby;
        }

        public static void GetLobbyInfo(ulong lobbyId, ref Lobby lobby) {
            lobby.LobbyId = (CSteamID) lobbyId;
            lobby.OwnerId = SteamMatchmaking.GetLobbyOwner(lobby.LobbyId);
            lobby.Members = new LobbyMember[SteamMatchmaking.GetNumLobbyMembers(lobby.LobbyId)];
            lobby.MemberLimit = SteamMatchmaking.GetLobbyMemberLimit(lobby.LobbyId);

            for(int i = 0; i < lobby.Members.Length; i++) {
                lobby.Members[i].SteamId = SteamMatchmaking.GetLobbyMemberByIndex(lobby.LobbyId, i);
                lobby.Members[i].Name = SteamFriends.GetFriendPersonaName(lobby.Members[i].SteamId);
                // TODO: Maybe add member data, but not required for now
            }

            int nDataCount = SteamMatchmaking.GetLobbyDataCount(lobby.LobbyId);
            lobby.Data = new LobbyMetaData[nDataCount];
            for(int i = 0; i < nDataCount; ++i) {
                bool lobbyDataRet = SteamMatchmaking.GetLobbyDataByIndex(lobby.LobbyId, i, out lobby.Data[i].Key, Constants.k_nMaxLobbyKeyLength, out lobby.Data[i].Value, Constants.k_cubChatMetadataMax);
                if(!lobbyDataRet) {
                    continue;
                }
            }
        }
    }
}
