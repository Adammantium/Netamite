using Netamite.Datatype;
using System.Collections.Generic;

namespace Netamite.Converter.Implementation {
    public class ColorConverter : DataConverterInterface<Color> {
        public static new Color FromBytes(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            Color result = new Color(
                FloatConverter.FromBytes(value, out _, startIndex                               , lowPrecision), 
                FloatConverter.FromBytes(value, out _, startIndex + (2 * (lowPrecision ? 1 : 2)), lowPrecision),
                FloatConverter.FromBytes(value, out _, startIndex + (4 * (lowPrecision ? 1 : 2)), lowPrecision),
                FloatConverter.FromBytes(value, out _, startIndex + (6 * (lowPrecision ? 1 : 2)), lowPrecision)
            );
            bytesRead = 8 * (lowPrecision ? 1 : 2);

            return result;
        }

        public static new byte[] GetBytes(Color value, bool lowPrecision = false) {
            List<byte> bytes = new List<byte>();
            bytes.AddRange(FloatConverter.GetBytes(value.r, lowPrecision));
            bytes.AddRange(FloatConverter.GetBytes(value.g, lowPrecision));
            bytes.AddRange(FloatConverter.GetBytes(value.b, lowPrecision));
            bytes.AddRange(FloatConverter.GetBytes(value.a, lowPrecision));

            return bytes.ToArray();
        }
    }
}
