using Netamite.Network.Packet.Attributes;

namespace Netamite.Network.Packet.Implementations {
    [PacketDefinition((byte) PacketType.MESSAGE)]
    public class MessagePacket : NetPacket {
        [SyncedVar] public string Message = "";

        public MessagePacket() { }

        public MessagePacket(string Message) {
            this.Message = Message;
        }
    }
}
