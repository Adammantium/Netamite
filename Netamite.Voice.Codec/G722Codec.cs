using System;
using SIPSorcery.Media;

namespace Netamite.Voice.Codec {
    public class G722Codec : AudioCodec {

        public string Name => "G.722 16kHz";

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

        public byte[] Decode(byte[] data) {
            short[] outputBuffer = new short[data.Length * 2];
            
            int decoded = codec.Decode(decoderState, outputBuffer, data, data.Length);
            
            byte[] boutputBuffer = new byte[decoded];
            Buffer.BlockCopy(outputBuffer, 0, boutputBuffer, 0, decoded);

            //Log.Debug($"decoded {decoded} | {outputBuffer.Length} | {boutputBuffer.Length}");
            
            return boutputBuffer;
        }

        public byte[] Encode(byte[] data) {
            byte[] outputBuffer = new byte[data.Length * 2];
            short[] sdata = new short[(int)Math.Ceiling(data.Length / 2f)];
            Buffer.BlockCopy(data, 0, sdata, 0, data.Length);
            
            int encoded = codec.Encode(encoderState, outputBuffer, sdata, data.Length / 2);
            
            byte[] finalBuffer = new byte[encoded];
            Array.Copy(outputBuffer, finalBuffer, encoded);
            
            //Log.Debug($"encoded {encoded} | {sdata.Length} | {outputBuffer.Length} | {finalBuffer.Length}");
            return finalBuffer;
        }
    }
}
