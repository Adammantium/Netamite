using Netamite.Client.Implementation;
using Netamite.Logging;
using Netamite.Network.Packet;
using Netamite.Network.Packet.Implementations;
using Netamite.Network.Socket;
using Netamite.Server.Data;
using Netamite.Server.Definition;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Netamite.Server.Implementation {
    public class IPServer : NetamiteServer {

        private string _bindingAddress = "0.0.0.0";
        private short tcpPort = 13698;
        private short udpPort = 13698;
        /// <summary>
        /// Sets the Port for both the TCP and UDP connection
        /// </summary>
        public short Port {
            set { tcpPort = value; udpPort = value; }
        }
        /// <summary>
        /// Sets and Gets the Port for the TCP connection
        /// </summary>
        public short TcpPort {
            set { tcpPort = value; }
            get { return tcpPort; }
        }
        /// <summary>
        /// Sets and Gets the Port for the UDP connection
        /// </summary>
        public short UdpPort { 
            set { udpPort = value; }
            get { return udpPort; }
        }

        private static TcpListener tcpListener;
        private static UdpClient udpListener;

        internal ConcurrentDictionary<NetSocket, long> waitingForConnectionSockets = new ConcurrentDictionary<NetSocket, long>();
        internal Dictionary<ulong, TcpSocket> tcpConnections = new Dictionary<ulong, TcpSocket>();
        internal Dictionary<ulong, UdpSocket> udpConnections = new Dictionary<ulong, UdpSocket>();

        public IPServer() : base() { }
        public IPServer(short port) : base() {
            Port = port;
        }
        public IPServer(short port, int maxClients) : this(port) {
            MaxClients = maxClients;
        }
        public IPServer(string bindingAddress, short port) : this(port) {
            _bindingAddress = bindingAddress;
        }
        public IPServer(string bindingAddress, short port, int maxClients) : this(bindingAddress, port) {
            MaxClients = maxClients;
        }

        public override void Start() {
            base.Start();

            tcpListener = new TcpListener(IPAddress.Any, TcpPort);
            tcpListener.Start();
            tcpListener.BeginAcceptTcpClient(TCPRequestCallback, null);

            udpListener = new UdpClient(UdpPort);
            udpListener.BeginReceive(UDPRequestCallback, null);
        }


        private void TCPRequestCallback(IAsyncResult result) {
            if(tcpListener == null) return;
            TcpClient tcpClient;
            try
            {
                tcpClient = tcpListener.EndAcceptTcpClient(result);
            } catch(ObjectDisposedException) { return; } // Happens when closing a already disposed socket, so we can just ignore it
            tcpListener.BeginAcceptTcpClient(TCPRequestCallback, null);
            
            TcpSocket socket = new TcpSocket(tcpClient);

            Log.Debug(Defines.SERVER, $"Incoming connection from {tcpClient.Client.RemoteEndPoint}... (" + waitingForConnectionSockets.Count + " still waiting)");
            waitingForConnectionSockets.TryAdd(socket, DateTimeOffset.Now.ToUnixTimeMilliseconds());
            socket.onPacket += (packet) => {
                WaitForConnection(socket, packet);
            };
        }

        private void UDPRequestCallback(IAsyncResult result) {
            if(udpListener == null) return;
            try {
                IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = null;
                try {
                    data = udpListener.EndReceive(result, ref clientEndPoint);
                } catch(Exception) { }
                udpListener.BeginReceive(UDPRequestCallback, null);

                if(data == null || data.Length <= 1) return;

                // Check if its a welcome package and if the user is not linked up
                using(NetPacket packet = NetPacket.ReadPacket(data, true)) {
                    if(packet is WelcomePacket) {
                        ulong clientId = ((WelcomePacket)packet).ClientId;

                        // If no udp is connected, then link up
                        if(!udpConnections.ContainsKey(clientId) && _clients.ContainsKey(clientId)) {
                            UdpSocket udpSocket = new UdpSocket(clientEndPoint);
                            udpConnections.Add(clientId, new UdpSocket(clientEndPoint));

                            ClientInformation clientInformation = _clients[clientId];
                            udpSocket.onPacket = null;

                            Log.Debug("[Server] Linked UDP for " + clientInformation.ClientName + " (" + clientEndPoint + ")");
                            return;
                        }
                    }
                }

                // Determine client id by EndPoint
                bool found = false;
                foreach(KeyValuePair<ulong, UdpSocket> entry in udpConnections) {
                    if(IPEndPoint.Equals(entry.Value.endPoint, clientEndPoint)) {
                        NetPacket netPacket = NetPacket.ReadPacket(data, true);
                        if(!(netPacket is WelcomePacket)) {
                            ProcessPacket(_clients[entry.Key], netPacket);
                        }
                        found = true;
                        break;
                    }
                }
                if(!found) {
                    //Log.Err(Defines.SERVER, $"Invalid UDP client tried to connect {clientEndPoint}");
                }
            } catch(Exception e) {
                Log.Err(Defines.SERVER, $"Error receiving UDP data: {e}");
            }
        }

        private void WaitForConnection(TcpSocket socket, NetPacket p) {
            if(p is EstablishConnectionPacket) {
                EstablishConnectionPacket ecp = (EstablishConnectionPacket) p;

                string error = CheckEstablishConnection(ecp);
                if(error == null) {
                    if(waitingForConnectionSockets.ContainsKey(socket)) waitingForConnectionSockets.TryRemove(socket, out _);
                    
                    ClientInformation clientInformation = new ClientInformation();
                    clientInformation.ClientName = ecp.name;

                    tcpConnections.Add(currentId + 1, socket);

                    socket.onPacket = null;
                    socket.onPacket += (packet) => {
                        ProcessPacket(clientInformation, packet);
                    };

                    ClientJoined(clientInformation);
                } else {
                    if(waitingForConnectionSockets.ContainsKey(socket)) waitingForConnectionSockets.TryRemove(socket, out _);
                    socket.QueuePacket(new ErrorPacket(error));
                    socket.Disconnect();
                    return;
                }
            }
        }

        internal override void ClientLeft(ClientInformation client) {
            if(tcpConnections.ContainsKey(client.ClientId)) {
                SendReliableTo(client, new DisconnectPacket(client.ClientId, "Connection closed"));
                tcpConnections.Remove(client.ClientId);
            }
            if(udpConnections.ContainsKey(client.ClientId)) {
                udpConnections.Remove(client.ClientId);
            }
            SendReliableToAllExcept(new DisconnectPacket(client.ClientId, "Connection closed"), client.ClientId);

            base.ClientLeft(client);
        }

        // Reliable
        public override void SendReliableTo(ClientInformation client, NetPacket p) {
            if(tcpConnections.ContainsKey(client.ClientId)) {
                tcpConnections[client.ClientId].SendPacket(p);
            }
        }

        // Unreliable
        public override void SendUnreliableTo(ClientInformation client, NetPacket p) {
            if(udpConnections.ContainsKey(client.ClientId)) {
                tcpConnections[client.ClientId].SendPacket(p);
            }
        }
    }
}
