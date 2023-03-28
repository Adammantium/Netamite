using Netamite.Network.Packet.Attributes;

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
