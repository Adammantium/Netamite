using System;

namespace Netamite.Network.Packet {
    [AttributeUsage(AttributeTargets.Class)]
    public class PacketDefinition : Attribute {
        public bool reliable = true;
        public byte packetType = 0;

        public PacketDefinition(PacketType packetType) {
            this.packetType = (byte) packetType;
        }

        public PacketDefinition(byte packetType) {
            this.packetType = packetType;
        }

        public PacketDefinition(bool reliable, byte packetType) : this(packetType) {
            this.reliable   = reliable;
        }

        public PacketDefinition(bool reliable, PacketType packetType) : this(packetType) {
            this.reliable = reliable;
        }
    }
}
