using System;

namespace Netamite.Converter.Implementation {
    public class ShortConverter : DataConverterInterface<short> {
        public static new short FromBytes(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            short result = BitConverter.ToInt16(value, startIndex);
            bytesRead = 2;
            return result;
        }

        public static new byte[] GetBytes(short value, bool lowPrecision = false) {
            return BitConverter.GetBytes(value);
        }
    }
}
