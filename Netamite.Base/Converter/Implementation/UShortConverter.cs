using System;
using System.Text;

namespace Netamite.Converter.Implementation {
    public class UShortConverter : DataConverterInterface<ushort> {
        public static new ushort FromBytes(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            //Logging.Log.Debug(ToHexString(value) + " " + startIndex);
            ushort result = BitConverter.ToUInt16(value, startIndex);
            bytesRead = 2;
            return result;
        }

        public static new byte[] GetBytes(ushort value, bool lowPrecision = false) {
            //Logging.Log.Debug(ToHexString(BitConverter.GetBytes(value)));
            return BitConverter.GetBytes(value);
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
