using NAudio.Codecs;
using NAudio.Wave;

namespace Netamite.Voice.Codec {
    public class G722Codec : AudioCodec {

        public string Name => "G.722 16kHz";

        private readonly int bitrate;
        private readonly G722CodecState encoderState;
        private readonly G722CodecState decoderState;
        private readonly NAudio.Codecs.G722Codec codec;

        public WaveFormat RecordFormat { get; }

        public G722Codec() {
            bitrate = 48000;
            encoderState = new G722CodecState(bitrate, G722Flags.None);
            decoderState = new G722CodecState(bitrate, G722Flags.None);
            codec = new NAudio.Codecs.G722Codec();
            RecordFormat = new WaveFormat(16000, 16, 1);
        }

        public byte[] Decode(byte[] data) {
            int decodedLength = data.Length * 4;
            var outputBuffer = new byte[decodedLength];
            var wb = new WaveBuffer(outputBuffer);
            int decoded = codec.Decode(decoderState, wb.ShortBuffer, data, data.Length);

            return outputBuffer;
        }

        public byte[] Encode(byte[] data) {
            var wb = new WaveBuffer(data);
            int encodedLength = data.Length / 4;
            var outputBuffer = new byte[encodedLength];
            int decoded = codec.Encode(encoderState, outputBuffer, wb.ShortBuffer, data.Length / 2);

            return outputBuffer;
        }
    }
}
