using Netamite.Network.Packet;
using System;

namespace Netamite.Server.Data {
    public class ClientInformation {

        public ulong    ClientId;
        public string   ClientName;
        public DateTime ConnectionTimestamp;

        internal long lastTimeSyncStamp;

        public Action<NetPacket> sendReliablePacket;
        public Action<NetPacket> sendUnreliablePacket;

    }
}
