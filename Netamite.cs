using Netamite.Client.Definition;
using Netamite.Client.Implementation;
using Netamite.Helper;
using Netamite.Logging;
using Netamite.Network.Packet.Implementations;
using Netamite.Server.Definition;
using Netamite.Server.Implementation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Netamite {
    public class Netamite {
        public static void Main(string[] args) {
            Console.WriteLine("  _   _      _                  _ _       \r\n | \\ | |    | |                (_) |      \r\n |  \\| | ___| |_ __ _ _ __ ___  _| |_ ___ \r\n | . ` |/ _ \\ __/ _` | '_ ` _ \\| | __/ _ \\\r\n | |\\  |  __/ || (_| | | | | | | | ||  __/\r\n |_| \\_|\\___|\\__\\__,_|_| |_| |_|_|\\__\\___|\r\n                                          \r\n                                          ");

            short port = 12345;

            NetamiteServer server = new IPServer(port);
            server.OnConnect += (ci) => {
                Log.Info(Defines.SERVER, "+ " + ci.ClientName);
            };
            server.OnDisconnect += (ci) => {
                Log.Info(Defines.SERVER, "- " + ci.ClientName);
            };
            server.OnDataReceived += (ci, packet) => {
                Log.Info(Defines.SERVER, "> " + ci.ClientName + " " + packet);
            };
            server.OnStart += (delay) => {
                Log.Info($"Started after {delay}ms.");
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
        }

        private static void Connect(int id, string address, short port) {
            //Thread.Sleep(id);
            NetamiteClient client = new IPClient(address, port);
            client.ClientName = "Client " + id;

            client.onConnect += () => {
                Log.Info(Defines.CLIENT + " " + id, "CONNECTED");
            };
            client.onConnectionError += (message) => {
                Log.Info(Defines.CLIENT + " " + id, "CONNECTION REFUSED: " + message);
            };
            client.onDisconnect += () => {
                Log.Info(Defines.CLIENT + " " + id, "DISCONNECTED");
            };
            client.OnDataReceived += (packet) => {
                Log.Info(Defines.CLIENT + " " + id, "> " + packet);
            };
            client.Connect("");

            Thread.Sleep(1000);
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
