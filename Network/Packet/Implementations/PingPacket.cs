using Netamite.Logging;
using Netamite.Network.Packet.Attributes;
using Netamite.Server.Data;
using System;

namespace Netamite.Network.Packet.Implementations {
    [PacketDefinition((byte) PacketType.PING)]
    public class PingPacket : NetPacket {
        [SyncedVar] public long timestamp = 0;

        public PingPacket() {
            if(timestamp <= 0) {
                timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            }
        }

        public PingPacket(long timestamp) {
            this.timestamp = timestamp;
        }
    }
}
