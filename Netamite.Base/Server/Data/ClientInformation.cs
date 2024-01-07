using Netamite.Helper;
using System;

namespace Netamite.Server.Data {
    public class ClientInformation {

        public int      ClientId;
        private string _ClientName;
        public string ClientName {
            get { 
                if(string.IsNullOrWhiteSpace(_ClientName)) {
                    return "User" + ClientId;
                }
                return _ClientName;
            }
            set { _ClientName = value; }
        }
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
