using Netamite.Steam.Integration;

namespace NetamiteTest {
    internal class SteamTest {

        public static void DoTest() {

            SteamIntegration.Initialize(480);

            while (true) {
                SteamIntegration.RunCallbacks();
            }
        }

    }
}
