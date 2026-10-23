using System;
using SIPSorcery.Media;

namespace Netamite.Voice.Codec {
    public class G722Codec : AudioCodec {

        public override string Name => "G.722 16kHz";

        private readonly int bitrate;
        private readonly G722CodecState encoderState;
        private readonly G722CodecState decoderState;
        private SIPSorcery.Media.G722Codec codec;

        public G722Codec() {
            bitrate = 48000;
            encoderState = new G722CodecState(bitrate, G722Flags.None);
            decoderState = new G722CodecState(bitrate, G722Flags.None);
            codec = new SIPSorcery.Media.G722Codec();
        }

        public override AudioCodec CreateNew() {
            return new G722Codec();
        }

        public override int Decode(byte[] data, ref byte[] outputBuffer) {
            ensurePcmBufferSize(ref decodePcmBuffer, data.Length * 2);
            
            int decoded = codec.Decode(decoderState, decodePcmBuffer, data, data.Length); // Number of samples
            
            ensureOutputBufferSize(ref outputBuffer, decoded * 2);
            Buffer.BlockCopy(decodePcmBuffer, 0, outputBuffer, 0, decoded * 2);

            //Log.Debug($"decoded {decoded} | {outputBuffer.Length} | {boutputBuffer.Length}");
            
            return decoded * 2;
        }

        public override int Encode(byte[] data, ref byte[] outputBuffer) {
            ensurePcmBufferSize(ref encodePcmBuffer, (int)Math.Ceiling(data.Length / 2f));
            ensureOutputBufferSize(ref outputBuffer, data.Length);
            
            Buffer.BlockCopy(data, 0, encodePcmBuffer, 0, data.Length);
            
            int encoded = codec.Encode(encoderState, outputBuffer, encodePcmBuffer, data.Length / 2);
            
            //Log.Debug($"encoded {encoded} | {sdata.Length} | {outputBuffer.Length} | {finalBuffer.Length}");
            return encoded;
        }
    }
}
