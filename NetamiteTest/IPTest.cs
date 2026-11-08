using Netamite;
using Netamite.Client.Definition;
using Netamite.Client.Implementation;
using Netamite.Logging;
using Netamite.Network.Packet.Implementations;
using Netamite.Server.Definition;
using Netamite.Server.Implementation;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NetamiteTest {
    internal class IPTest {

        internal static void DoTest() {
            ushort port = 12345;

            NetamiteServer server = new IPServer(port);
            server.OnConnect += (ci) => {
                Log.Info(Defines.SERVER, "+ " + ci.ClientName);
            };
            server.OnDisconnect += (ci, reason) => {
                Log.Info(Defines.SERVER, "- " + ci.ClientName + " " + reason);
            };
            server.OnDataReceived += (ci, packet) => {
                Log.Info(Defines.SERVER, "REC " + ci.ClientName + " " + packet);

                server.SendUnreliableToAll(new MessagePacket("test"));
            };
            server.OnStart += (delay) => {
                Log.Info(Defines.SERVER, $"Started after {delay}ms.");
            };
            server.OnStop += () => {
                Log.Info(Defines.SERVER, "Server closed");
            };
            server.ConnectToken = "";
            server.Start();

            var tasks = new List<Task>();
            for(int i = 0; i < 1; i++) {
                int n = i + 1;
                tasks.Add(Task.Factory.StartNew(() => Connect(n, "127.0.0.1", port)));
            }

            //then wait for all tasks to complete asyncronously
            Task.WaitAll(tasks.ToArray());

            server.Stop();
        }

        private static void Connect(int id, string address, ushort port) {
            Thread.Sleep(id * 200);
            NetamiteClient client = new IPClient(address, port);
            client.ClientName = "Client " + id;

            client.OnConnect += () => {
                Log.Info(Defines.CLIENT + " " + id, "CONNECTED");
            };
            client.OnConnectionError += (message) => {
                Log.Info(Defines.CLIENT + " " + id, "CONNECTION REFUSED: " + message);
            };
            client.OnDisconnect += (reason) => {
                Log.Info(Defines.CLIENT + " " + id, "Disconnect: " + reason);
            };
            client.OnDataReceived += (packet) => {
                Log.Info(Defines.CLIENT + " " + id, "> " + packet);
            };
            client.Connect("");

            Thread.Sleep(1000);
            if(client.IsConnected) {
                Log.Debug(Defines.CLIENT + " " + id, "RELIABLE");
                client.SendReliable(new MessagePacket("TCP WHOOP"));

                Thread.Sleep(500);
                for(int i = 0; i < 10; i++) {
                    //Log.Debug(Defines.CLIENT + " " + id, "UNRELIABLE");
                    //client.SendUnreliable(new MessagePacket("UDP WHOOP"));
                    Thread.Sleep(1000);
                }
                Thread.Sleep(500);

                client.Disconnect();
                Thread.Sleep(1000);
            }
        }
    }
}
