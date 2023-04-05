using Netamite.Logging;
using Netamite.Network.Packet;
using Netamite.Network.Socket;
using Steamworks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Netamite.Steam.Socket {
    public class SteamSocket : NetSocket {

        public Action<ulong, NetPacket> onPacketWithId;
        private ConcurrentDictionary<ulong, List<byte>> packet_buffer = new ConcurrentDictionary<ulong, List<byte>>();

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
            byte[] data = packet.GetData(true);

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
                uint size;
                CSteamID sender;
                if(SteamNetworking.IsP2PPacketAvailable(out _, channel)) {
                    SteamNetworking.ReadP2PPacket(data, (uint) data.Length, out size, out sender, channel);

                    if((ulong) sender == 0) continue;
                    if(size == 0) continue;
                    if(data.Length == 0) continue;

                    HandleData((ulong) sender, data.Take((int) size).ToArray());
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
            List<byte> buffer = packet_buffer.GetOrAdd(sender, new List<byte>());

            lock(buffer) { 
                buffer.AddRange(data);
            }

            //Log.Warn("REC BUFF " + ByteArrayToString(buffer.ToArray()));
            while(buffer.Count > 3) { // At least 3 bytes are needed 2 bytes for length, and 1 byte for the packet type
                ushort length = BitConverter.ToUInt16(new byte[] { buffer[0], buffer[1] }, 0);
                //Log.Debug("SOCKET", length + " / " + buffer.Count + " / Packet Id: " + buffer[2]); // REMOVE

                if(length == 0 || buffer[2] == 0) {
                    lock(buffer) {
                        buffer.RemoveAt(0);
                    }
                    continue;
                }

                if(buffer.Count >= length + 2) {
                    //Log.Warn($"REC T{buffer[2]} | L:{length}");

                    byte[] packet_data = buffer.GetRange(2, length).ToArray();

                    bytesReceived += length + 2;
                    //Log.Debug("SOCKET", "Packet Data " + packet_data.Length); // REMOVE
                    try {
                        using(NetPacket packet = NetPacket.ReadPacket(packet_data)) {
                            if(packet != null) {
                                //Log.Warn("SOCKET", "ReceivedPacket " + sender + " " + packet); // REMOVE
                                onPacketWithId?.Invoke(sender, packet);
                            }
                        }

                        lock(buffer) {
                            buffer.RemoveRange(0, length + 2);
                        }
                    } catch(System.Exception e) {
                        Log.Err(e);
                        lock(buffer) {
                            buffer.RemoveRange(0, length + 2);
                        }
                        break;
                    }
                } else {
                    break;
                }
            }
        }
    }
}
