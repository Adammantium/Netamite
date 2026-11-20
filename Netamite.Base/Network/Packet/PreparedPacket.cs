using System;

namespace Netamite.Network.Packet {
    /// <summary>
    /// A packet that goes out to multiple receivers. It gets converted to bytes once, by whoever needs the bytes
    /// first, and everybody else reuses them. Without it a packet for 10 clients would be serialized 10 times.
    /// </summary>
    public sealed class PreparedPacket {
        /// <summary>
        /// The packet itself, for transports that have no use for the bytes (like handing it to a local client)
        /// </summary>
        public readonly NetPacket Packet;

        private volatile byte[] data;

        public PreparedPacket(NetPacket packet) {
            if(packet == null) throw new ArgumentNullException(nameof(packet));
            Packet = packet;
        }

        /// <summary>
        /// The packet the way it goes over the wire, including the length in front. Do not modify the array, it is shared.
        /// </summary>
        public byte[] GetData() {
            byte[] result = data;
            if(result != null) return result;

            lock(this) {
                if(data == null) data = Packet.GetData(true);
                return data;
            }
        }
    }
}
