namespace Netamite.Converter {
    public class DataConverterInterface<T> {
        /// <summary>
        /// Gets the binary data of the value
        /// </summary>
        /// <param name="value">The object you want to convert to binary</param>
        /// <param name="lowPrecision">If possible write the value with a low precision</param>
        /// <returns>Binary Data</returns>
        public static byte[] GetBytes(T value, bool lowPrecision = false) {
            return null;
        }

        public static T FromBytes(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            bytesRead = 0;
            return default(T);
        }
    }
}
