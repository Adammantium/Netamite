using Netamite.Network.Packet.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netamite.Network.Packet.Implementations {
    [PacketDefinition((byte) PacketType.WELCOME)]
    public class WelcomePacket : NetPacket {
        [SyncedVar] public ulong ClientId;

        public WelcomePacket() { }

        public WelcomePacket(ulong playerId) {
            this.ClientId = playerId;
        }
    }
}
