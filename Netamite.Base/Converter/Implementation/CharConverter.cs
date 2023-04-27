using System;

namespace Netamite.Converter.Implementation {
    public class CharConverter : DataConverterInterface<char> {
        public static new char FromData(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            char result = BitConverter.ToChar(value, startIndex);
            bytesRead = 1;
            return result;
        }

        public static new byte[] GetData(char value, bool lowPrecision = false) {
            return BitConverter.GetBytes(value);
        }
    }
}
