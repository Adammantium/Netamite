using Netamite.Client.Definition;
using Netamite.Network.Packet;
using Netamite.Network.Packet.Attributes;
using Netamite.Server.Data;
using Netamite.Server.Definition;
using Netamite.Unity.Voice;

namespace Netamite.Unity.Voice.Packet {
    [PacketDefinition(false, PacketType.AUDIO)]
    public class UnityAudioPacket : NetPacket {
        [SyncedVar] public int channelId;
        [SyncedVar] public byte[] voiceData;

        public UnityAudioPacket() { }

        public UnityAudioPacket(int channelId, byte[] voiceData) {
            this.channelId  = channelId;
            this.voiceData = voiceData;
        }

        public override bool ProcessClient(NetamiteClient client) {
            UnityVoiceClient.instance?.OnData(channelId, voiceData);
            return true;
        }

        public override bool ProcessServer(NetamiteServer server, ClientInformation client) {
            server.SendToAllExcept(this, client.ClientId);
            return true;
        }
    }
}
