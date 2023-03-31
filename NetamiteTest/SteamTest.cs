using Netamite.Client.Definition;
using Netamite.Client.Implementation;
using Netamite.Logging;
using Netamite.Network.Packet.Implementations;
using Netamite;
using Netamite.Server.Definition;
using Netamite.Server.Implementation;
using Netamite.Steam.Integration;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Netamite.Steam.Client;
using Netamite.Steam.Server;
using Netamite.Steam.Datatype;

namespace NetamiteTest {
    internal class SteamTest {

        public static void DoTest() {
            SteamIntegration.Initialize(480);

            NetamiteServer server = new SteamServer(10, true);
            server.OnConnect += (ci) => {
                Log.Info(Defines.SERVER, "+ " + ci.ClientName);
            };
            server.OnDisconnect += (ci, reason) => {
                Log.Info(Defines.SERVER, "- " + ci.ClientName + " " + reason);
            };
            server.OnDataReceived += (ci, packet) => {
                Log.Info(Defines.SERVER, "> " + ci.ClientName + " " + packet);
            };
            server.OnStart += (delay) => {
                Log.Info(Defines.SERVER, $"Started after {delay}ms.");
            };
            server.OnClose += () => {
                Log.Info(Defines.SERVER, "Server closed");
                SteamIntegration.Shutdown();
            };
            server.ConnectToken = "";
            server.Start();

            Thread.Sleep(2500);

            BeginConnecting((ulong)((SteamServer)server).currentLobby.LobbyId);

            SteamIntegration.Shutdown();
        }

        private static void BeginConnecting(ulong lobbyId) {

            var tasks = new List<Task>();
            for(int i = 0; i < 1; i++) {
                int n = i + 1;
                tasks.Add(Task.Factory.StartNew(() => Connect(n, lobbyId)));
            }

            Task.WaitAll(tasks.ToArray());
        }

        private static void Connect(int id, ulong lobbyId) {
            Thread.Sleep(id * 200);
            NetamiteClient client = new SteamClient(lobbyId);;
            //client.ClientName = "Client " + id;

            client.onConnect += () => {
                Log.Info(Defines.CLIENT + " " + id, "CONNECTED");
            };
            client.onConnectionError += (message) => {
                Log.Info(Defines.CLIENT + " " + id, "CONNECTION REFUSED: " + message);
            };
            client.onDisconnect += (reason) => {
                Log.Info(Defines.CLIENT + " " + id, "Disconnect: " + reason);
            };
            client.OnDataReceived += (packet) => {
                Log.Info(Defines.CLIENT + " " + id, "> " + packet);
            };
            client.Connect("");

            Thread.Sleep(2500);
            if(client.isConnected) {
                Log.Debug(Defines.CLIENT + " " + id, "SEND TCP");
                client.SendReliable(new PingPacket());

                Thread.Sleep(500);
                Log.Debug(Defines.CLIENT + " " + id, "SEND UDP");
                client.SendUnreliable(new PingPacket());
                Thread.Sleep(500);

                client.Disconnect();
                Thread.Sleep(1000);
            }
        }
    }
}
