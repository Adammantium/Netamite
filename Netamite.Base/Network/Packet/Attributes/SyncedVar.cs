using System;

namespace Netamite.Network.Packet.Attributes {
    [AttributeUsage(AttributeTargets.Field)]
    public class SyncedVar : Attribute {

        public bool LowPrecision = false;

        public SyncedVar() { }

        public SyncedVar(bool LowPrecision) {
            this.LowPrecision = LowPrecision;
        }

    }
}
