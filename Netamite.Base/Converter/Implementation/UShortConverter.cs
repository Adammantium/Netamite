using System;

namespace Netamite.Converter.Implementation {
    public class UShortConverter : DataConverterInterface<ushort> {
        public static new ushort FromData(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            ushort result = BitConverter.ToUInt16(value, startIndex);
            bytesRead = 2;
            return result;
        }

        public static new byte[] GetData(ushort value, bool lowPrecision = false) {
            return BitConverter.GetBytes(value);
        }
    }
}
