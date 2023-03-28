using AMP.Network.Helper;
using Netamite.Client.Definition;
using Netamite.Logging;
using Netamite.Network.Packet;
using Netamite.Network.Packet.Implementations;
using Netamite.Network.Socket;
using System.Net;
using System.Threading;

namespace Netamite.Client.Implementation {
    internal class IPClient : NetamiteClient {

        private string ip;
        private int port;

        private TcpSocket tcp;
        private UdpSocket udp;

        public override string TYPE => "SOCKET";

        public IPClient(string address, int port) : base() {
            this.ip = NetworkUtil.GetIP(address);
            this.port = port;
        }

        public override void Connect(string accessToken = "") {
            base.Connect(accessToken);

            Log.Info(Defines.CLIENT, $"Connecting to {ip}:{port}...");

            tcp = new TcpSocket(ip, port);
            tcp.onPacket = AwaitingConnectionHandler;
            tcp.onDisconnect = onConnectionAbort;

            if(!tcp.client.Connected) {
                _STATE = "FAILED";
                Disconnect();
                return;
            } else {
                onConnect += OnConnect;
                onConnect += LinkUDP;
                EstablishConnection();
            }
        }

        private void OnConnect() {
            if(tcp != null) {
                tcp.onPacket = null;
                tcp.onPacket = onTcpPacketReceived;
            }
            onConnect -= OnConnect;
        }

        private void LinkUDP() {
            udp = new UdpSocket(ip, port);
            udp.Connect(((IPEndPoint) tcp.client.Client.LocalEndPoint).Port);
            udp.onPacket = onUdpPacketReceived;

            // Send some udp packets, one should reach the host if ports are free
            Thread udpLinkThread = new Thread(() => {
                for(int i = 0; i < 20; i++) {
                    if(udp == null) break;
                    udp.QueuePacket(new WelcomePacket(ClientId));
                    Thread.Sleep(100);
                }
            });
            udpLinkThread.Name = "UdpLinker";
            udpLinkThread.Start();

            onConnect -= LinkUDP;
        }

        private void onConnectionAbort() {
            Log.Err(Defines.CLIENT, "Error at connection resulted in disconnect.");
            Disconnect();
        }

        public override void Disconnect() {
            if(tcp != null) {
                tcp.onDisconnect -= onConnectionAbort;
                tcp.QueuePacket(new DisconnectPacket(0, "Connection closed"));
                tcp.Disconnect();
                tcp = null;
            }
            if(udp != null) {
                udp.onDisconnect -= onConnectionAbort;
                udp.Disconnect();
                udp = null;
            }
            onConnect -= OnConnect;
            onConnect -= LinkUDP;

            base.Disconnect();
        }

        public override void SendReliable(NetPacket packet) {
            if(tcp != null) {
                tcp.QueuePacket(packet);
                base.SendReliable(packet);
            }
        }

        public override void SendUnreliable(NetPacket packet) {
            if(udp != null) {
                udp.QueuePacket(packet);
                base.SendUnreliable(packet);
            }
        }

        internal void onTcpPacketReceived(NetPacket p) {
            base.OnReliableReceived(p);
        }

        internal void onUdpPacketReceived(NetPacket p) {
            base.OnUnreliableReceived(p);
        }
    }
}
