using Netamite.Client.Definition;
using Netamite.Client.Implementation;
using Netamite.Helper;
using Netamite.Logging;
using Netamite.Network.Packet.Implementations;
using Netamite.Server.Definition;
using Netamite.Server.Implementation;
using System;
using System.Diagnostics;
using System.Threading;

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



            NetamiteClient client = new IPClient("127.0.0.1", port);
            client.ClientName = "Client 1";

            client.onConnect += () => {
                Log.Info(Defines.CLIENT, "CONNECTED");
            };
            client.onConnectionError += (message) => {
                Log.Info(Defines.CLIENT, "CONNECTION REFUSED: " + message);
            };
            client.onDisconnect += () => {
                Log.Info(Defines.CLIENT, "DISCONNECTED");
            };
            client.OnDataReceived += (packet) => {
                Log.Info(Defines.CLIENT, "> " + packet);
            };
            client.Connect("");

            while (true) {
                Thread.Sleep(100);
                if(client.isConnected) {
                    Log.Debug("SEND TCP");
                    for(int i = 0; i < 100; i++) {
                        client.SendReliable(new PingPacket());
                    }

                    Thread.Sleep(500);
                    Log.Debug("SEND UDP");
                    client.SendUnreliable(new PingPacket());
                    Thread.Sleep(500);

                    client.Disconnect();
                }
            }
        }
    }
}
