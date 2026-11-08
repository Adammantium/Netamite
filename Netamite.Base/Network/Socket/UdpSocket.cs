using Netamite.Logging;
using Netamite.Network.Helper;
using Netamite.Network.Packet;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Netamite.Network.Socket {
    internal class UdpSocket : NetSocket {

        internal IPEndPoint endPoint;
        internal UdpClient client;

        public override string TYPE => "UDP";
        public override bool IsConnected => true;

        internal UdpSocket(IPEndPoint endPoint) {
            this.endPoint = endPoint;
        }

        internal UdpSocket(string ip, int port) {
            endPoint = new IPEndPoint(IPAddress.Parse(NetworkUtil.GetIP(ip)), port);
        }

        internal void Connect(int localPort) {
            client = new UdpClient(localPort);

            client.Connect(endPoint);

            StartProcessing();
        }

        public override void Disconnect() {
            base.Disconnect();

            if(client != null) client.Close();
            client = null;
            endPoint = null;

            onPacket = null;
        }

        public override void SendPacket(NetPacket packet) {
            try {
                if(client != null) {
                    byte[] data = packet.GetData(true);

                    bytesSent += data.Length;

                    client.SendAsync(data, data.Length);
                }
            } catch(Exception e) {
                Log.Err($"Error sending data to {endPoint} via UDP: {e}");
            }
        }

        public override void AwaitData() {
            client.BeginReceive(new AsyncCallback(ReceiveCallback), null);
        }

        private void ReceiveCallback(IAsyncResult _result) {
            try {
                if(client != null && _result != null) {
                    // Read data
                    byte[] array = client.EndReceive(_result, ref this.endPoint);

                    if(array.Length < 3) {
                        Disconnect();
                        return;
                    }
                    HandleData(array);
                }
            } catch(SocketException e) {
                Log.Err("Failed to receive data with udp, " + e);
                Disconnect();
            } catch(ObjectDisposedException) { return; } catch(IOException) { return; }

            if(client != null)
                client.BeginReceive(new AsyncCallback(this.ReceiveCallback), null);
        }

    }
}
