using Netamite.Network;
using System;

namespace Netamite.Converter.Implementation {
    public class LongConverter : DataConverterInterface<long> {
        public static new long FromBytes(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            long result = BitConverter.ToInt64(value, startIndex);
            bytesRead = 8;
            return result;
        }

        public static new byte[] GetBytes(long value, bool lowPrecision = false) {
            return BitConverter.GetBytes(value);
        }

        public override void Write(BinaryNetStream stream, long value, bool lowPrecision) {
            stream.WriteLong(value);
        }

        public override long Read(BinaryNetStream stream, bool lowPrecision) {
            return stream.ReadLong();
        }
    }
}
