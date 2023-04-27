using Netamite.Datatype;
using System.Collections.Generic;

namespace Netamite.Converter.Implementation {
    internal class ColorConverter : DataConverterInterface<Color> {
        public static new Color FromData(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            Color result = new Color(
                (float) DataConverter.FromData(typeof(float), value, startIndex                               , lowPrecision), 
                (float) DataConverter.FromData(typeof(float), value, startIndex + (2 * (lowPrecision ? 1 : 2)), lowPrecision),
                (float) DataConverter.FromData(typeof(float), value, startIndex + (4 * (lowPrecision ? 1 : 2)), lowPrecision),
                (float) DataConverter.FromData(typeof(float), value, startIndex + (6 * (lowPrecision ? 1 : 2)), lowPrecision)
            );
            bytesRead = 8 * (lowPrecision ? 1 : 2);

            return result;
        }

        public static new byte[] GetData(Color value, bool lowPrecision = false) {
            List<byte> bytes = new List<byte>();
            bytes.AddRange(FloatConverter.GetData(value.r, lowPrecision));
            bytes.AddRange(FloatConverter.GetData(value.g, lowPrecision));
            bytes.AddRange(FloatConverter.GetData(value.b, lowPrecision));
            bytes.AddRange(FloatConverter.GetData(value.a, lowPrecision));

            return bytes.ToArray();
        }
    }
}
