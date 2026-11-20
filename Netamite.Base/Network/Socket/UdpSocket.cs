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
            // Serialized into the buffer of this thread and handed to the socket from there, no array per packet needed
            BinaryNetStream stream = BinaryNetStream.RentWriter();
            try {
                UdpClient current = client;
                if(current != null) {
                    packet.WriteTo(stream, true);
                    int length = stream.Length();

                    bytesSent += length;

                    current.Client.Send(stream.RawBuffer, stream.RawOffset, length, SocketFlags.None);
                }
            } catch(SocketException) {
                // Nobody reachable on the other side, UDP makes no promises
            } catch(ObjectDisposedException) {
                // Socket got closed in this very moment
            } catch(Exception e) {
                Log.Err($"Error sending data to {endPoint} via UDP: {e}");
            } finally {
                BinaryNetStream.ReturnWriter(stream);
            }
        }

        public override void SendPrepared(PreparedPacket packet) {
            try {
                UdpClient current = client;
                if(current != null) {
                    byte[] data = packet.GetData();

                    bytesSent += data.Length;

                    current.Client.Send(data, 0, data.Length, SocketFlags.None);
                }
            } catch(SocketException) {
            } catch(ObjectDisposedException) {
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

                    // Every datagram stands for itself, a broken one must not affect the ones that follow
                    HandleDatagram(array, 0, array.Length);
                }
            } catch(SocketException e) {
                Log.Err("Failed to receive data with udp, " + e);
                Disconnect();
            } catch(ObjectDisposedException) { return; } catch(IOException) { return; }

            try {
                UdpClient current = client;
                if(current != null)
                    current.BeginReceive(new AsyncCallback(this.ReceiveCallback), null);
            } catch(ObjectDisposedException) {
                // Socket got closed while the packets were handled
            } catch(SocketException e) {
                Log.Err("Failed to receive data with udp, " + e);
                Disconnect();
            }
        }

    }
}
