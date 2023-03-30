using System;

namespace Netamite.Network.Packet.Exceptions {
    [Serializable]
    public class PacketMismatch : Exception {
        public PacketMismatch(PacketType mine, PacketType supplied) 
            : base("Invalid Packet Type, Parser: " + mine + ", Supplied: " + supplied)
            { }
    }
}
