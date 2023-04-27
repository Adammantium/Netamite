namespace Netamite.Converter.Implementation {
    public class ByteConverter : DataConverterInterface<byte> {
        public static new int FromData(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            int result = value[startIndex];
            bytesRead = 1;
            return result;
        }

        public static new byte[] GetData(byte value, bool lowPrecision = false) {
            return new byte[1] { value };
        }
    }
}
