using Netamite.Network.Packet.Attributes;

namespace Netamite.Network.Packet.Implementations {
    [PacketDefinition((byte) PacketType.WELCOME)]
    public class WelcomePacket : NetPacket {
        [SyncedVar] public int ClientId;

        public WelcomePacket() { }

        public WelcomePacket(int ClientId) {
            this.ClientId = ClientId;
        }
    }
}
