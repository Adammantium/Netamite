using Netamite.Logging;
using Netamite.Network.Packet;
using Netamite.Network.Socket;
using Steamworks;
using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;

namespace Netamite.Steam.Socket {
    public class SteamSocket : NetSocket {

        public Action<ulong, NetPacket> onPacketWithId;

        /// <summary>
        /// Data of one sender that could not be processed yet, because the packet it belongs to isn't complete
        /// </summary>
        private class SenderBuffer {
            public byte[] data = new byte[1024];
            public int count = 0;
        }
        private ConcurrentDictionary<ulong, SenderBuffer> packet_buffer = new ConcurrentDictionary<ulong, SenderBuffer>();

        CSteamID target;
        EP2PSend mode;
        int channel;

        public override bool IsConnected => true;

        public SteamSocket(CSteamID target, EP2PSend mode, int channel) {
            this.target = target;
            this.mode = mode;
            this.channel = channel;
        }

        public override void SendPacket(NetPacket packet) {
            SendBytes(packet.GetData(true));
        }

        public override void SendPacket(PreparedPacket packet) {
            SendBytes(packet.GetData()); // Already serialized if the packet went to somebody else before
        }

        private void SendBytes(byte[] data) {
            byte attempts = 50;
            //if(mode == EP2PSend.k_EP2PSendUnreliable || mode == EP2PSend.k_EP2PSendUnreliableNoDelay) attempts = 0;

            bytesSent += data.Length;
            while(!SteamNetworking.SendP2PPacket(target, data, (uint) data.Length, mode, channel)) {
                attempts--;
                if(attempts <= 0) break;
                Thread.Sleep(1);
            }
            //Log.Warn($"SND T{data[2]} | L:{data.Length - 2}");
            //Log.Warn("SND BUFF " + ByteArrayToString(data));
        }

        public override void AwaitData() {
            if(awaitDataThread != null) return;
            awaitDataThread = new Thread(ReadSocket);
            awaitDataThread.Name = "SteamSocket Data Read Thread";
            awaitDataThread.Start();

            //Log.Err("SteamSocket Data Read Thread");
        }

        private void ReadSocket() {
            byte[] data = new byte[4096];
            while(!closing) {
                // Read everything that is waiting before going to sleep. Sleeping after every single packet limits
                // the socket to a few hundred packets per second, everything above that piles up inside of Steam.
                uint available;
                while(!closing && SteamNetworking.IsP2PPacketAvailable(out available, channel)) {
                    if(available > data.Length) data = new byte[available]; // Steam cuts off whatever doesn't fit

                    uint size;
                    CSteamID sender;
                    if(!SteamNetworking.ReadP2PPacket(data, (uint) data.Length, out size, out sender, channel)) break; // Somebody else was faster

                    if((ulong) sender == 0) continue;
                    if(size == 0) continue;

                    HandleData((ulong) sender, data, (int) size);
                }
                Thread.Sleep(1);
            }
        }

        public static string ByteArrayToString(byte[] ba) {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach(byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }

        internal void HandleData(ulong sender, byte[] data) {
            HandleData(sender, data, data.Length);
        }

        /// <summary>
        /// Processes data a user sent. The array is free to be reused once this returns.
        /// </summary>
        internal void HandleData(ulong sender, byte[] data, int count) {
            SenderBuffer buffer;
            if(!packet_buffer.TryGetValue(sender, out buffer)) {
                buffer = packet_buffer.GetOrAdd(sender, new SenderBuffer());
            }

            lock(buffer) {
                if(buffer.count == 0) {
                    // The usual case: the data contains complete packets, they can be read right where they are
                    int used = ReadPackets(sender, data, count);
                    if(used < count) Append(buffer, data, used, count - used);
                } else {
                    Append(buffer, data, 0, count);

                    int used = ReadPackets(sender, buffer.data, buffer.count);
                    buffer.count -= used;
                    if(buffer.count > 0) Buffer.BlockCopy(buffer.data, used, buffer.data, 0, buffer.count);
                }
            }
        }

        private static void Append(SenderBuffer buffer, byte[] data, int offset, int count) {
            if(buffer.count + count > buffer.data.Length) {
                Array.Resize(ref buffer.data, Math.Max(buffer.data.Length * 2, buffer.count + count));
            }
            Buffer.BlockCopy(data, offset, buffer.data, buffer.count, count);
            buffer.count += count;
        }

        /// <summary>
        /// Reads all complete packets (ushort length, byte type, members) and passes them on
        /// </summary>
        /// <returns>How many bytes have been processed</returns>
        private int ReadPackets(ulong sender, byte[] data, int count) {
            int pos = 0;

            //Log.Warn("REC BUFF " + ByteArrayToString(buffer.ToArray()));
            while(count - pos >= 3) { // At least 3 bytes are needed 2 bytes for length, and 1 byte for the packet type
                int length = data[pos] | (data[pos + 1] << 8);
                //Log.Debug("SOCKET", length + " / " + buffer.Count + " / Packet Id: " + buffer[2]); // REMOVE

                if(length == 0 || data[pos + 2] == 0) {
                    pos++; // Can't be the start of a packet, look for it one byte further
                    continue;
                }

                if(count - pos < length + 2) break; // Rest of the packet is still on its way

                //Log.Warn($"REC T{buffer[2]} | L:{length}");

                bytesReceived += length + 2;
                int packetStart = pos + 2;
                pos += length + 2;

                //Log.Debug("SOCKET", "Packet Data " + packet_data.Length); // REMOVE
                try {
                    using(NetPacket packet = NetPacket.ReadPacket(data, packetStart, length)) {
                        if(packet != null) {
                            //Log.Warn("SOCKET", "ReceivedPacket " + sender + " " + packet); // REMOVE
                            onPacketWithId?.Invoke(sender, packet);
                        }
                    }
                } catch(System.Exception e) {
                    Log.Err(e);
                    break;
                }
            }
            return pos;
        }
    }
}
