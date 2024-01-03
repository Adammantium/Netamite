using NAudio.Wave;
using NAudio.Wave.Compression;
using System;

namespace Netamite.Voice.Codec {
    public class AcmCodec : AudioCodec {

        public WaveFormat RecordFormat { get; }

        public string Name => "ACM Base";

        private readonly WaveFormat encodeFormat;

        private AcmStream encodeStream;
        private AcmStream decodeStream;
        private int decodeSourceBytesLeftovers;
        private int encodeSourceBytesLeftovers;

        protected AcmCodec(WaveFormat recordFormat, WaveFormat encodeFormat) {
            RecordFormat = recordFormat;
            this.encodeFormat = encodeFormat;
        }

        public byte[] Decode(byte[] data) {
            if(decodeStream == null) {
                decodeStream = new AcmStream(encodeFormat, RecordFormat);
            }

            return Convert(decodeStream, data, 0, data.Length, ref decodeSourceBytesLeftovers);
        }

        public byte[] Encode(byte[] data) {
            if(encodeStream == null) {
                encodeStream = new AcmStream(RecordFormat, encodeFormat);
            }

            return Convert(encodeStream, data, 0, data.Length, ref encodeSourceBytesLeftovers);
        }

        private static byte[] Convert(AcmStream conversionStream, byte[] data, int offset, int length, ref int sourceBytesLeftovers) {
            int bytesInSourceBuffer = length + sourceBytesLeftovers;
            Array.Copy(data, offset, conversionStream.SourceBuffer, sourceBytesLeftovers, length);
            int bytesConverted = conversionStream.Convert(bytesInSourceBuffer, out var sourceBytesConverted);
            sourceBytesLeftovers = bytesInSourceBuffer - sourceBytesConverted;
            if(sourceBytesLeftovers > 0) {
                Array.Copy(conversionStream.SourceBuffer, sourceBytesConverted, conversionStream.SourceBuffer, 0, sourceBytesLeftovers);
            }
            byte[] encoded = new byte[bytesConverted];
            Array.Copy(conversionStream.DestBuffer, 0, encoded, 0, bytesConverted);
            return encoded;
        }
    }
}
