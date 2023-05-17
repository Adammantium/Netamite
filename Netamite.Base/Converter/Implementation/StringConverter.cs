using System.Collections.Generic;
using System.Text;

namespace Netamite.Converter.Implementation {
    public class StringConverter : DataConverterInterface<string> {
        public static new string FromBytes(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            int num = IntConverter.FromBytes(value, out bytesRead, startIndex, lowPrecision);

            string result;
            if(num <= 0) {
                result = "";
            } else {
                string str = Encoding.UTF8.GetString(value, startIndex + 4, num);
                bytesRead += num;
                result = str;
            }
            return result;
        }

        public static new byte[] GetBytes(string value, bool lowPrecision = false) {
            byte[] str_bytes = Encoding.UTF8.GetBytes(value);

            List<byte> bytes = new List<byte>();
            bytes.AddRange(IntConverter.GetBytes(str_bytes.Length));

            if(str_bytes.Length > 0) bytes.AddRange(str_bytes);

            return bytes.ToArray();
        }
    }
}
