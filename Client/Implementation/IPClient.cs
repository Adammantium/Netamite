using AMP.Network.Helper;
using Netamite.Client.Definition;
using Netamite.Logging;
using Netamite.Network.Packet;
using Netamite.Network.Packet.Implementations;
using Netamite.Network.Socket;
using Netamite.Security;
using System;
using System.Net;
using System.Threading;

namespace Netamite.Client.Implementation {
    internal class IPClient : NetamiteClient {

        private string ip;
        private int port;
        private string password = "";

        private TcpSocket tcp;
        private UdpSocket udp;

        public override string TYPE => "SOCKET";

        public IPClient(string address, int port) {
            this.ip = NetworkUtil.GetIP(address);
            this.port = port;
        }

        public override void Connect(string password = "") {
            _STATE = "CONNECTING";

            Log.Info(Defines.CLIENT, $"Connecting to {ip}:{port}...");
            this.password = Hashing.SHA256(password);

            tcp = new TcpSocket(ip, port);
            tcp.onPacket += AwaitingConnectionHandler;
            tcp.onDisconnect += onConnectionAbort;

            isConnected = tcp.client.Connected;
            if(!isConnected) {
                _STATE = "FAILED";
                Disconnect();
            } else {
                tcp.QueuePacket(new EstablishConnectionPacket("NAME", this.password));
                onConnect += LinkUDP;
            }

            onConnect += OnConnect;
        }

        private void OnConnect() {
            tcp.onPacket = null;
            tcp.onPacket = OnPacketReceived;
        }

        private void LinkUDP() {
            udp = new UdpSocket(ip, port);
            udp.Connect(((IPEndPoint) tcp.client.Client.LocalEndPoint).Port);
            udp.onPacket = OnPacketReceived;

            // Send some udp packets, one should reach the host if ports are free
            Thread udpLinkThread = new Thread(() => {
                for(int i = 0; i < 20; i++) {
                    udp.QueuePacket(new WelcomePacket(ClientId));
                    Thread.Sleep(100);
                }
            });
            udpLinkThread.Name = "UdpLinker";
            udpLinkThread.Start();
        }

        private void onConnectionAbort() {
            Log.Err(Defines.CLIENT, "Error at connection resulted in disconnect.");
            Disconnect();
        }

        internal void onTcpPacketReceived(NetPacket p) {
            OnPacketReceived?.Invoke(p);
            reliableReceive += p.GetData().Length;
        }

        internal void onUdpPacketReceived(NetPacket p) {
            OnPacketReceived?.Invoke(p);
            unreliableReceive += p.GetData().Length;
        }

        public override void Disconnect() {
            if(tcp != null) {
                tcp.onDisconnect -= onConnectionAbort;
                tcp.QueuePacket(new DisconnectPacket(0, "Connection closed"));
                tcp.Disconnect();
            }
            if(udp != null) {
                udp.onDisconnect -= onConnectionAbort;
                udp.Disconnect();
            }
            Log.Info(Defines.CLIENT, "Disconnected.");
            base.Disconnect();
        }

        public override void SendReliable(NetPacket packet) {
            tcp.QueuePacket(packet);
            reliableSent += packet.GetData().Length;
        }

        public override void SendUnreliable(NetPacket packet) {
            udp.QueuePacket(packet);
            unreliableSent += packet.GetData().Length;
        }
    }
}
