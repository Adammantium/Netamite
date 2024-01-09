using Netamite.Client.Definition;
using Netamite.Network.Packet;
using Netamite.Network.Packet.Attributes;
using Netamite.Server.Data;
using Netamite.Server.Definition;

namespace Netamite.Voice.Packet {
    [PacketDefinition(false, PacketType.AUDIO)]
    public class AudioPacket : NetPacket {
        [SyncedVar] public int channelId;
        [SyncedVar] public byte[] voiceData;

        public AudioPacket() { }

        public AudioPacket(int channelId, byte[] voiceData) {
            this.channelId  = channelId;
            this.voiceData = voiceData;
        }

        public override bool ProcessClient(NetamiteClient client) {
            VoiceClient.instance?.OnData(channelId, voiceData);
            return true;
        }

        public override bool ProcessServer(NetamiteServer server, ClientInformation client) {
            server.SendToAllExcept(this, client.ClientId);
            return true;
        }
    }
}
