using Netamite.Client.Implementation;
using Netamite.Logging;
using Netamite.Network.Packet;
using Netamite.Network.Socket;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Timers;

namespace Netamite.Steam.Socket {
    public class SteamSocket : NetSocket {

        public Action<ulong, NetPacket> onPacketWithId;
        private Dictionary<ulong, List<byte>> packet_buffer = new Dictionary<ulong, List<byte>>();

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

            byte attempts = 5;
            if(mode == EP2PSend.k_EP2PSendUnreliable || mode == EP2PSend.k_EP2PSendUnreliableNoDelay) attempts = 0;

            while(!SteamNetworking.SendP2PPacket(target, data, (uint) data.Length, mode, channel)) {
                attempts--;
                if(attempts <= 0) break;
                Thread.Sleep(1);
            }
        }

        public override void AwaitData() {
            if(awaitDataThread != null) return;
            awaitDataThread = new Thread(ReadSocket);
            awaitDataThread.Name = "SteamSocket Data Read Thread";
            awaitDataThread.Start();

            //Log.Err("SteamSocket Data Read Thread");
        }

        private void ReadSocket() {
            while(!closing) {
                uint size;
                byte[] data;
                CSteamID sender;
                if(SteamNetworking.IsP2PPacketAvailable(out size, channel)) {
                    data = new byte[size];
                    SteamNetworking.ReadP2PPacket(data, size, out size, out sender, channel);

                    if((ulong) sender == 0) continue;
                    if(size == 0) continue;
                    if(data.Length == 0) continue;

                    HandleData((ulong) sender, data);
                }
                Thread.Sleep(1);
            }
        }

        internal void HandleData(ulong sender, byte[] data) {
            if(!packet_buffer.ContainsKey(sender)) {
                packet_buffer.Add(sender, new List<byte>());
            }

            List<byte> buffer = packet_buffer[sender];

            if(buffer == null) return;

            buffer.AddRange(data);

            while(buffer.Count > 3) { // At least 3 bytes are needed 2 bytes for length, and 1 byte for the packet type
                short length = BitConverter.ToInt16(new byte[] { buffer[0], buffer[1] }, 0);

                //Log.Debug("SOCKET", length + " / " + buffer.Count + " / Packet Id: " + buffer[2]); // REMOVE

                if(length <= 0 || buffer[2] == 0) {
                    buffer.Clear();
                    break;
                }

                if(buffer.Count >= length + 2) {
                    byte[] packet_data = buffer.GetRange(2, length).ToArray();

                    //Log.Debug("SOCKET", "Packet Data " + packet_data.Length); // REMOVE
                    try {
                        using(NetPacket packet = NetPacket.ReadPacket(packet_data)) {
                            if(packet != null) {
                                //Log.Warn("SOCKET", "ReceivedPacket " + sender + " " + packet); // REMOVE
                                onPacketWithId?.Invoke(sender, packet);
                            }
                        }

                        buffer.RemoveRange(0, length + 2);
                    } catch(System.Exception e) {
                        Log.Err(e);
                        buffer.RemoveRange(0, length + 2);
                        break;
                    }
                } else {
                    break;
                }
            }
        }
    }
}
