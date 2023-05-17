using System;

namespace Netamite.Converter.Implementation {
    public class BooleanConverter : DataConverterInterface<bool> {
        public static new bool FromBytes(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            bool result = BitConverter.ToBoolean(value, startIndex);
            bytesRead = 1;
            return result;
        }

        public static new byte[] GetBytes(bool value, bool lowPrecision = false) {
            return BitConverter.GetBytes(value);
        }
    }
}
