using Netamite.Network.Packet;
using Netamite.Network.Socket;
using Netamite.Server.Data;
using Netamite.Server.Definition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

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

            //waitingForConnectionSockets.TryAdd(socket, DateTimeOffset.Now.ToUnixTimeMilliseconds());
            //socket.onPacket += (packet) => {
            //    WaitForConnection(socket, packet);
            //};
        }

        private void UDPRequestCallback(IAsyncResult result) {
            
        }


        // Reliable
        public override void SendReliableTo(ClientInformation client, NetPacket p) {
            // TODO
        }

        // Unreliable
        public override void SendUnreliableTo(ClientInformation client, NetPacket p) {
            // TODO
        }
    }
}
