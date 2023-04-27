using System;

namespace Netamite.Converter.Implementation {
    public class IntConverter : DataConverterInterface<int> {
        public static new int FromData(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            int result = BitConverter.ToInt32(value, startIndex);
            bytesRead = 4;
            return result;
        }

        public static new byte[] GetData(int value, bool lowPrecision = false) {
            return BitConverter.GetBytes(value);
        }
    }
}
