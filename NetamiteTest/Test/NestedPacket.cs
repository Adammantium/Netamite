using Netamite.Network.Packet;
using Netamite.Network.Packet.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetamiteTest.Test {
    [PacketDefinition(255)]
    public class NestedPacket : NetPacket {
        [SyncedVar] public string Name;
        [SyncedVar] public NestedClass nestedClass;
        [SyncedVar] public NestedClass[] nestedClasses;

        [Serializable]
        public class NestedClass {
            [SyncedVar] public string Name;
        }
    }
}
