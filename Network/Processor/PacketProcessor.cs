using Netamite.Client.Definition;
using Netamite.Network.Packet;
using Netamite.Server.Data;
using Netamite.Server.Definition;

namespace Netamite.Network.Processor {
    public class PacketProcessor {

        public bool ProcessPacket(NetPacket packet, NetamiteClient client) {
            return packet.ProcessClient(client);
        }

        public bool ProcessPacket(NetPacket packet, NetamiteServer server, ClientInformation client) {
            return packet.ProcessServer(server, client);
        }

    }
}
