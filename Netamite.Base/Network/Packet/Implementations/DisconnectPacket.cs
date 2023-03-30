using Netamite.Client.Definition;
using Netamite.Network.Packet.Attributes;
using Netamite.Server.Data;
using Netamite.Server.Definition;

namespace Netamite.Network.Packet.Implementations {
    [PacketDefinition((byte) PacketType.DISCONNECT)]
    public class DisconnectPacket : NetPacket {
        [SyncedVar] public int ClientId = 0;
        [SyncedVar] public string Reason = "";

        public DisconnectPacket() { }

        public DisconnectPacket(int ClientId, string Reason) {
            this.ClientId = ClientId;
            this.Reason = Reason;
        }

        public override bool ProcessClient(NetamiteClient client) {
            if(client.ClientId == ClientId) {
                client.Disconnect();
                return true;
            }
            return base.ProcessClient(client);
        }

        public override bool ProcessServer(NetamiteServer server, ClientInformation client) {
            server.ClientLeft(client);
            return true;
        }
    }
}
