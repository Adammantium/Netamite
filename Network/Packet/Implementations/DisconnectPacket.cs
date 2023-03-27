using Netamite.Network.Packet.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netamite.Network.Packet.Implementations {
    [PacketDefinition((byte) PacketType.DISCONNECT)]
    public class DisconnectPacket : NetPacket {
        [SyncedVar] public long playerId;
        [SyncedVar] public string reason;

        public DisconnectPacket() { }

        public DisconnectPacket(long playerId, string reason) {
            this.playerId = playerId;
            this.reason = reason;
        }
    }
}
