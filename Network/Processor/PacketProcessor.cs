using Netamite.Client.Definition;
using Netamite.Network.Packet;
using Netamite.Server.Data;
using Netamite.Server.Definition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
