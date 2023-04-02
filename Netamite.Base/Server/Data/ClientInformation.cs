using Netamite.Helper;
using System;

namespace Netamite.Server.Data {
    public class ClientInformation {

        public int      ClientId;
        public string   ClientName;
        public DateTime ConnectionTimestamp;

        internal long lastTimeSyncStamp;

        internal long lastPacketTimestamp;

        public ClientInformation() {
            lastPacketTimestamp = TimeHelper.Millis;
        }
    }
}
