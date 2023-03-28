using Netamite.Client.Definition;
using Netamite.Logging;
using Netamite.Network.Packet.Attributes;
using Netamite.Server.Data;
using Netamite.Server.Definition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netamite.Network.Packet.Implementations {
    [PacketDefinition((byte) PacketType.TIME_SYNCHRONIZATION)]
    public class TimeSynchronizationPacket : NetPacket {
        [SyncedVar] public long server_timestamp = 0;

        public TimeSynchronizationPacket() {
            if(server_timestamp <= 0) {
                server_timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            }
        }

        public TimeSynchronizationPacket(long server_timestamp) {
            this.server_timestamp = server_timestamp;
        }

        public override bool ProcessClient(NetamiteClient client) {
            // It's the acknowledgment
            // Latency compensation is based on the assumption that the time of the packet in one direction is equal to the round trip time divided by 2.
            if(server_timestamp == client.lastServerTimestamp) { 
                long ping_rtt = DateTimeOffset.Now.ToUnixTimeMilliseconds() - client.serverTimestampTime;
                long offset = client.lastServerTimestamp - client.serverTimestampTime + (ping_rtt / 2);

                client.serverTimestampOffset = offset;
                //Log.Debug($"Recalculated Time Offset to server: {client.serverTimestampOffset}ms");
                return true;
            } else { // Fist Time Sync Request with that timestamp
                client.lastServerTimestamp = server_timestamp;
                client.serverTimestampTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

                client.SendReliable(this);
                return true;
            }
        }

        public override bool ProcessServer(NetamiteServer server, ClientInformation client) {
            if(client.lastTimeSyncStamp == server_timestamp) {
                server.SendReliableTo(client, this);
                return true;
            } else {
                Log.Warn(Defines.SERVER, $"{client.ClientName} responded to an outdated Time-Synchronization request. Can be ignored if player just connected.");
                return true;
            }
        }
    }
}
