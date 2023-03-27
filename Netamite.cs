using Netamite.Client.Definition;
using Netamite.Client.Implementation;
using Netamite.Logging;
using Netamite.Network.Packet.Implementations;
using Netamite.Server.Definition;
using Netamite.Server.Implementation;
using System;
using System.Threading;

namespace Netamite {
    public class Netamite {
        public static void Main(string[] args) {
            Console.WriteLine("  _   _      _                  _ _       \r\n | \\ | |    | |                (_) |      \r\n |  \\| | ___| |_ __ _ _ __ ___  _| |_ ___ \r\n | . ` |/ _ \\ __/ _` | '_ ` _ \\| | __/ _ \\\r\n | |\\  |  __/ || (_| | | | | | | | ||  __/\r\n |_| \\_|\\___|\\__\\__,_|_| |_| |_|_|\\__\\___|\r\n                                          \r\n                                          ");

            IPServer server = new IPServer();

            server.OnConnect += (ci) => {
                Console.WriteLine("+ " + ci.ClientName);
            };
            server.OnDisconnect += (ci) => {
                Console.WriteLine("- " + ci.ClientName);
            };
            server.OnDataReceived += (ci, packet) => {
                Console.WriteLine("> " + ci.ClientName + " " + packet);
            };
            server.ConnectToken = "";
            server.Start();



            NetamiteClient client = new IPClient("127.0.0.1", server.TcpPort);
            
            client.onConnect += () => {
                Console.WriteLine("CONNECTED");
            };
            client.onDisconnect += () => {
                Console.WriteLine("DISCONNECTED");
            };

            client.Connect("");



            while (true) {
                //Log.Err(client.isConnected);
                Thread.Sleep(100);
                if(client.isConnected) {
                    Log.Debug("SEND TCP");
                    client.SendReliable(new PingPacket(0));

                    Thread.Sleep(500);
                    Log.Debug("SEND UDP");
                    client.SendUnreliable(new PingPacket(0));
                    Thread.Sleep(500);
                }
            }
        }
    }
}
