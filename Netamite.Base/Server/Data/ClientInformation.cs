using Netamite.Helper;
using System;

namespace Netamite.Server.Data {
    public class ClientInformation {

        public int      ClientId;
        public string   ClientName;
        public DateTime ConnectionTimestamp;

        internal long lastTimeSyncStamp;
        internal long nextTimeSyncStamp;
        internal byte secondDelayBetweenTimeSync = 0;

        internal long lastPacketTimestamp;

        public ClientInformation() {
            lastPacketTimestamp = TimeHelper.Millis;
        }
    }
}
