using System;
using SIPSorcery.Media;

namespace Netamite.Voice.Codec {
    public class G729Codec : AudioCodec {

        public override string Name => "G.729 16kHz";
        
        private readonly G729Encoder encoderState;
        private readonly G729Decoder decoderState;

        public G729Codec() {
            encoderState = new G729Encoder();
            decoderState = new G729Decoder();
        }

        public override AudioCodec CreateNew() {
            return new G729Codec();
        }

        public override int Decode(byte[] data, ref byte[] outputBuffer) {
            outputBuffer = decoderState.Process(data);
            
            return outputBuffer.Length;
        }

        public override int Encode(byte[] data, ref byte[] outputBuffer) {
            outputBuffer = encoderState.Process(data);
            
            return outputBuffer.Length;
        }
    }
}
