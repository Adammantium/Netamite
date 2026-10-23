namespace Netamite.Voice.Codec {
    public class UncompressedCodec : AudioCodec {
        public string Name => "Uncompressed 16khz";

        public byte[] Decode(byte[] data) {
            return data;
        }

        public byte[] Encode(byte[] data) {
            return data;
        }
    }
}
