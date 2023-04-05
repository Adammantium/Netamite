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





        internal long unreliablePacketsSent = 0;
        internal long reliablePacketsSent = 0;
        internal long unreliablePacketsReceive = 0;
        internal long reliablePacketsReceive = 0;

        public long GetPacketsSent() {
            long bytesSent = unreliablePacketsSent + reliablePacketsSent;

            unreliablePacketsSent = 0;
            reliablePacketsSent = 0;

            return bytesSent;
        }

        public long GetPacketsReceive() {
            long bytesReceive = unreliablePacketsReceive + reliablePacketsReceive;

            unreliablePacketsReceive = 0;
            reliablePacketsReceive = 0;

            return bytesReceive;
        }

        public virtual long GetBytesSent() { return 0; }
        public virtual long GetBytesReceive() { return 0; }

    }
}
