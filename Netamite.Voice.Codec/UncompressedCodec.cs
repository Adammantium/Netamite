using System;

namespace Netamite.Voice.Codec {
    public class UncompressedCodec : AudioCodec {
        public override string Name => "Uncompressed 16khz";

        public override AudioCodec CreateNew() {
            return new UncompressedCodec();
        }

        public override int Decode(byte[] data, ref byte[] outputBuffer) {
            ensureOutputBufferSize(ref outputBuffer, data.Length);
            Buffer.BlockCopy(data, 0, outputBuffer, 0, data.Length);
            return data.Length;
        }

        public override int Encode(byte[] data, ref byte[] outputBuffer) {
            ensureOutputBufferSize(ref outputBuffer, data.Length);
            Buffer.BlockCopy(data, 0, outputBuffer, 0, data.Length);
            return data.Length;
        }
    }
}
