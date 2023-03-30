using Netamite.Client.Definition;
using Netamite.Helper;
using Netamite.Network.Packet.Attributes;
using Netamite.Server.Data;
using Netamite.Server.Definition;

namespace Netamite.Network.Packet.Implementations {
    [PacketDefinition((byte) PacketType.PING)]
    public class PingPacket : NetPacket {
        [SyncedVar] public long timestamp = 0;

        public PingPacket() {
            if(timestamp <= 0) {
                timestamp = TimeHelper.Millis;
            }
        }

        public PingPacket(long timestamp) {
            this.timestamp = timestamp;
        }

        public override bool ProcessClient(NetamiteClient client) {
            if(timestamp > 0) {
                client._Ping = TimeHelper.Millis - timestamp;
            }
            return true;
        }

        public override bool ProcessServer(NetamiteServer server, ClientInformation client) {
            if(timestamp > 0) {
                server.SendTo(client, this);
            }
            return true;
        }
    }
}
