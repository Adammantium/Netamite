using Netamite.Network.Packet.Attributes;

namespace Netamite.Network.Packet.Implementations {
    [PacketDefinition((byte) PacketType.ESTABLISH_CONNECTION)]
    public class EstablishConnectionPacket : NetPacket {
        [SyncedVar] public string name = "";
        [SyncedVar] public string password = "";

        public EstablishConnectionPacket() { }

        public EstablishConnectionPacket(string name, string password) {
            this.name = name;
            this.password = password;
        }
    }
}
