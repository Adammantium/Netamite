using Netamite.Client.Definition;
using Netamite.Network.Packet.Attributes;
using Netamite.Server.Data;
using Netamite.Server.Definition;

namespace Netamite.Network.Packet.Implementations {
    [PacketDefinition((byte) PacketType.ERROR)]
    public class ErrorPacket : NetPacket {
        [SyncedVar] public string message;

        public ErrorPacket() { }

        public ErrorPacket(string error_message) {
            this.message = error_message;
        }
    }
}
