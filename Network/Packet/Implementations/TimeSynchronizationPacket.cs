using Netamite.Client.Definition;
using Netamite.Helper;
using Netamite.Logging;
using Netamite.Network.Packet.Attributes;
using Netamite.Server.Data;
using Netamite.Server.Definition;

namespace Netamite.Network.Packet.Implementations {
    [PacketDefinition((byte) PacketType.TIME_SYNCHRONIZATION)]
    public class TimeSynchronizationPacket : NetPacket {
        [SyncedVar] public long server_timestamp = 0;

        public TimeSynchronizationPacket() {
            if(server_timestamp <= 0) {
                server_timestamp = TimeHelper.Millis;
            }
        }

        public TimeSynchronizationPacket(long server_timestamp) {
            this.server_timestamp = server_timestamp;
        }

        public override bool ProcessClient(NetamiteClient client) {
            // It's the acknowledgment
            // Latency compensation is based on the assumption that the time of the packet in one direction is equal to the round trip time divided by 2.
            if(server_timestamp == client.lastServerTimestamp) { 
                long ping_rtt = TimeHelper.Millis - client.serverTimestampTime;
                long offset = client.lastServerTimestamp - client.serverTimestampTime + (ping_rtt / 2);

                client.serverTimestampOffset = offset;
                return true;
            } else { // Fist Time Sync Request with that timestamp
                client.lastServerTimestamp = server_timestamp;
                client.serverTimestampTime = TimeHelper.Millis;

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
