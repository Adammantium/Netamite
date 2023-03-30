using System;

namespace Netamite.Steam.Exception {
    internal class SteamNotDetectedException : SystemException {
        public override string Message => "Steam not found. Is Steam launched? Is the game in the library?";
    }
}
