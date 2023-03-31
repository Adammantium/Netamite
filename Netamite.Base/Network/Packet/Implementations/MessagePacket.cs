using Netamite.Client.Definition;
using Netamite.Datatype;
using Netamite.Logging;
using Netamite.Network.Packet.Attributes;
using Netamite.Server.Data;
using Netamite.Server.Definition;
using System.Drawing;

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
