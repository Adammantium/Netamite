using System;

namespace Netamite.Network.Packet {
    [AttributeUsage(AttributeTargets.Class)]
    public class PacketDefinition : Attribute {
        public byte packetType = 0;

        public PacketDefinition(byte packetType) {
            this.packetType = packetType;
        }
    }
}
