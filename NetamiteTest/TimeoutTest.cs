using Netamite;
using Netamite.Client.Definition;
using Netamite.Client.Implementation;
using Netamite.Logging;
using Netamite.Server.Definition;
using Netamite.Server.Implementation;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NetamiteTest {
    internal class TimeoutTest {

        internal static void DoTest() {
            short port = 12345;

            NetamiteServer server = new IPServer(port);
            server.ClientTimeoutDelay = 5000;
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

        private static void Connect(int id, string address, short port) {
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
            Console.Write("Waiting for connection to timeout");
            
            // Disable the automatic pinging of the server so the client will timeout
            client.PingDelay = 0;

            while(client.IsConnected) {
                Console.Write(".");
                Thread.Sleep(500);
            }
        }
    }
}
