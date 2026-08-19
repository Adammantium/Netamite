using System;
using System.Collections.Generic;
using System.Text;

namespace Netamite.Converter.Implementation {
    public class StringConverter : DataConverterInterface<string> {
        public static new string FromBytes(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            int num = UShortConverter.FromBytes(value, out bytesRead, startIndex, lowPrecision);

            //Logging.Log.Debug(ToHexString(value));
            
            string result;
            if(num <= 0) {
                result = "";
            } else {
                string str = Encoding.UTF8.GetString(value, startIndex + bytesRead, num);
                bytesRead += num;
                result = str;
                //Logging.Log.Debug(result);
            }
            return result;
        }

        public static new byte[] GetBytes(string value, bool lowPrecision = false) {
            if(value == null) value = "";
            
            byte[] str_bytes = Encoding.UTF8.GetBytes(value);

            List<byte> bytes = new List<byte>(UShortConverter.GetBytes((ushort) str_bytes.Length, lowPrecision));
            
            if(str_bytes.Length > 0) bytes.AddRange(str_bytes);
            
            //Logging.Log.Debug(value + " => " + ToHexString(bytes.ToArray()));
            
            return bytes.ToArray();
        }

        public static string ToHexString(byte[] bytes) {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            // Initialisiert den Speicher direkt mit der exakten Zielgröße (2 Zeichen pro Byte)
            StringBuilder hex = new StringBuilder(bytes.Length * 2);

            foreach (byte b in bytes) {
                hex.Append(b.ToString("X2")); // "X2" erzwingt zweistellige Großbuchstaben (z. B. 0A)
            }

            return hex.ToString();
        }
    }
}
