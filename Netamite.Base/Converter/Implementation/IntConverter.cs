using System;

namespace Netamite.Converter.Implementation {
    public class IntConverter : DataConverterInterface<int> {
        public static new int FromBytes(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            int result = BitConverter.ToInt32(value, startIndex);
            bytesRead = 4;
            return result;
        }

        public static new byte[] GetBytes(int value, bool lowPrecision = false) {
            return BitConverter.GetBytes(value);
        }
    }
}
