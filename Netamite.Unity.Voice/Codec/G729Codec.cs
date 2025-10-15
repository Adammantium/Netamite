using System;
using Netamite.Logging;
using Netamite.Unity.Voice.Audio;
using Netamite.Unity.Voice.Codec;
using SIPSorcery.Media;

namespace Netamite.Unity.Voice.Codec {
    public class G729Codec : AudioCodec {

        public string Name => "G.729 16kHz";
        
        private readonly G729Encoder encoderState;
        private readonly G729Decoder decoderState;

        public G729Codec() {
            encoderState = new G729Encoder();
            decoderState = new G729Decoder();
        }

        public byte[] Decode(byte[] data) {
            byte[] decoded = decoderState.Process(data);
            
            return decoded;
        }

        public byte[] Encode(byte[] data) {
            byte[] encoded = encoderState.Process(data);
            
#if MIRROR_BACK
            var decoded = Decode(encoded);
            NetamiteAudioSource nas = UnityVoiceClient.instance?.GetAudioPlayer(0);
            nas.QueuePlay(decoded);
#endif
            
            return encoded;
        }
    }
}
