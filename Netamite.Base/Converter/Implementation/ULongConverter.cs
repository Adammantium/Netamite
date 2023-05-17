using System;

namespace Netamite.Converter.Implementation {
    public class ULongConverter : DataConverterInterface<ulong> {
        public static new ulong FromBytes(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            ulong result = BitConverter.ToUInt64(value, startIndex);
            bytesRead = 8;
            return result;
        }

        public static new byte[] GetBytes(ulong value, bool lowPrecision = false) {
            return BitConverter.GetBytes(value);
        }
    }
}
