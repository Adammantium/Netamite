using System;

namespace Netamite.Converter.Implementation {
    public class LongConverter : DataConverterInterface<long> {
        public static new long FromData(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            long result = BitConverter.ToInt64(value, startIndex);
            bytesRead = 8;
            return result;
        }

        public static new byte[] GetData(long value, bool lowPrecision = false) {
            return BitConverter.GetBytes(value);
        }
    }
}
