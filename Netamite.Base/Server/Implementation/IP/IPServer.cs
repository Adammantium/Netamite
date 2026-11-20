using Netamite.Datatype;
using Netamite.Helper;
using Netamite.Logging;
using Netamite.Network;
using Netamite.Network.Packet;
using Netamite.Network.Packet.Implementations;
using Netamite.Network.Socket;
using Netamite.Server.Data;
using Netamite.Server.Definition;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Net.Sockets;

namespace Netamite.Server.Implementation {
    public class IPServer : NetamiteServer {

        private string _bindingAddress = "0.0.0.0";
        private ushort tcpPort = 13698;
        private ushort udpPort = 13698;
        /// <summary>
        /// Sets the Port for both the TCP and UDP connection
        /// </summary>
        public ushort Port {
            set { tcpPort = value; udpPort = value; }
        }
        /// <summary>
        /// Sets and Gets the Port for the TCP connection
        /// </summary>
        public ushort TcpPort {
            set { tcpPort = value; }
            get { return tcpPort; }
        }
        /// <summary>
        /// Sets and Gets the Port for the UDP connection
        /// </summary>
        public ushort UdpPort { 
            set { udpPort = value; }
            get { return udpPort; }
        }

        private static TcpListener tcpListener;
        private static UdpClient udpListener;

        internal ConcurrentDictionary<NetSocket, long> waitingForConnectionSockets = new ConcurrentDictionary<NetSocket, long>();
        internal ConcurrentDictionary<int, TcpSocket> tcpConnections = new ConcurrentDictionary<int, TcpSocket>();
        internal ConcurrentDictionary<int, IPEndPoint> udpEndpoints = new ConcurrentDictionary<int, IPEndPoint>();
        // The other way around, to find out who sent a datagram without comparing its address to the one of every client
        internal ConcurrentDictionary<IPEndPoint, int> udpClientIds = new ConcurrentDictionary<IPEndPoint, int>();

        public IPServer() : base() { }
        public IPServer(ushort port) : base() {
            Port = port;
        }
        public IPServer(ushort port, int maxClients) : this(port) {
            MaxClients = maxClients;
        }
        public IPServer(string bindingAddress, ushort port) : this(port) {
            _bindingAddress = bindingAddress;
        }
        public IPServer(string bindingAddress, ushort port, int maxClients) : this(bindingAddress, port) {
            MaxClients = maxClients;
        }

        public override void Start() {
            base.InvokeStartEvent(false);
            base.Start();

            tcpListener = new TcpListener(IPAddress.Any, TcpPort);
            tcpListener.Start();
            tcpListener.BeginAcceptTcpClient(TCPRequestCallback, null);

            udpListener = new UdpClient(UdpPort);
            udpListener.BeginReceive(UDPRequestCallback, null);
            base.InvokeStartEvent(true);
        }


        private void TCPRequestCallback(IAsyncResult result) {
            if(tcpListener == null) return;
            TcpClient tcpClient = null;
            try
            {
                tcpClient = tcpListener.EndAcceptTcpClient(result);
            } catch(ObjectDisposedException) { return; } // Happens when closing a already disposed socket, so we can just ignore it
            catch(SocketException e) {
                // A client that aborts mid handshake must not stop the accept loop
                Log.Warn(Defines.SERVER, $"Failed to accept incoming connection: {e.Message}");
            }

            // Keep accepting no matter what happens to this client. This runs on the thread pool, so any
            // uncaught exception kills the server process and every client gets refused until it restarts
            try {
                tcpListener?.BeginAcceptTcpClient(TCPRequestCallback, null);
            } catch(ObjectDisposedException) { return; }

            if(tcpClient == null) return;

            TcpSocket socket;
            try {
                socket = new TcpSocket(tcpClient);
                Log.Debug(Defines.SERVER, $"Incoming connection from {tcpClient.Client.RemoteEndPoint}... (" + waitingForConnectionSockets.Count + " still waiting)");
            } catch(Exception e) when(e is SocketException || e is IOException || e is ObjectDisposedException) {
                Log.Warn(Defines.SERVER, $"Incoming connection dropped during setup: {e.Message}");
                tcpClient.Close();
                return;
            }

            waitingForConnectionSockets.TryAdd(socket, TimeHelper.Millis);
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
                if(udpListener == null) return;
                udpListener.BeginReceive(UDPRequestCallback, null);

                if(data == null || data.Length <= 2) return;

                // Determine client id by EndPoint
                int clientId;
                if(udpClientIds.TryGetValue(clientEndPoint, out clientId)) {
                    ClientInformation client;
                    if(!_clients.TryGetValue(clientId, out client)) return;

                    NetPacket netPacket = NetPacket.ReadPacket(data, true);
                    if(!(netPacket is WelcomePacket)) {
                        ProcessPacket(client, netPacket);
                    }
                } else {
                    // Check if its a welcome package and if the user is not linked up
                    using(NetPacket packet = NetPacket.ReadPacket(data, true)) {
                        if(packet is WelcomePacket) {
                            clientId = ((WelcomePacket) packet).ClientId;

                            // If no udp is connected, then link up
                            ClientInformation clientInformation;
                            if(!udpEndpoints.ContainsKey(clientId) && _clients.TryGetValue(clientId, out clientInformation)) {
                                if(udpEndpoints.TryAdd(clientId, clientEndPoint)) {
                                    udpClientIds[clientEndPoint] = clientId;

                                    Log.Debug(Defines.SERVER, "Linked UDP for " + clientInformation.ClientName + " (" + clientEndPoint + ")");
                                }
                                return;
                            }
                        }
                    }
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
                    
                    ClientInformation clientInformation = (ClientInformation) Activator.CreateInstance(Netamite.GetClientinfoType());
                    clientInformation.ClientName = ecp.name;

                    clientInformation.ClientId = NextId;
                    tcpConnections.TryAdd(clientInformation.ClientId, socket);

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

        public override void ClientLeft(ClientInformation client, string reason = DisconnectReason.CONNECTION_CLOSED) {
            base.ClientLeft(client, reason);

            IPEndPoint udpEndpoint;
            if(udpEndpoints.TryRemove(client.ClientId, out udpEndpoint)) {
                // Only if the address still belongs to this client, somebody else could have taken it over in the meantime
                ((ICollection<KeyValuePair<IPEndPoint, int>>) udpClientIds).Remove(new KeyValuePair<IPEndPoint, int>(udpEndpoint, client.ClientId));
            }
            TcpSocket tcpSocket;
            if(tcpConnections.TryRemove(client.ClientId, out tcpSocket)) {
                tcpSocket.Disconnect();
            }
        }

        // Reliable
        public override void SendReliableTo(ClientInformation client, NetPacket p) {
            TcpSocket tcpSocket;
            if(tcpConnections.TryGetValue(client.ClientId, out tcpSocket)) {
                tcpSocket.QueuePacket(p);
            }
        }

        public override void SendReliableTo(ClientInformation client, PreparedPacket p) {
            TcpSocket tcpSocket;
            if(tcpConnections.TryGetValue(client.ClientId, out tcpSocket)) {
                tcpSocket.QueuePacket(p);
            }
        }

        // Unreliable
        public override void SendUnreliableTo(ClientInformation client, NetPacket p) {
            IPEndPoint endPoint;
            if(udpEndpoints.TryGetValue(client.ClientId, out endPoint)) {
                // Serialized into the buffer of this thread and handed to the socket from there, no array per packet needed
                BinaryNetStream stream = BinaryNetStream.RentWriter();
                try {
                    p.WriteTo(stream, true);
                    udpListener.Client.SendTo(stream.RawBuffer, stream.RawOffset, stream.Length(), SocketFlags.None, endPoint);
                } finally {
                    BinaryNetStream.ReturnWriter(stream);
                }
            }
        }

        public override void SendUnreliableTo(ClientInformation client, PreparedPacket p) {
            IPEndPoint endPoint;
            if(udpEndpoints.TryGetValue(client.ClientId, out endPoint)) {
                byte[] data = p.GetData();
                udpListener.Client.SendTo(data, 0, data.Length, SocketFlags.None, endPoint);
            }
        }

        public override void Stop() {
            InvokeStopEvent(false);

            tcpListener?.Stop();
            tcpListener = null;
            udpListener?.Close();
            udpListener = null;

            foreach(TcpSocket tcpSocket in tcpConnections.Values) {
                tcpSocket?.Disconnect();
            }

            InvokeStopEvent(true);
        }
    }
}
