using Netamite.Datatype;
using System.Collections.Generic;

namespace Netamite.Converter.Implementation {
    internal class Vector3Converter : DataConverterInterface<Vector3> {
        public static new Vector3 FromData(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            Vector3 result = new Vector3(
                (float) DataConverter.FromData(typeof(float), value, startIndex                               , lowPrecision), 
                (float) DataConverter.FromData(typeof(float), value, startIndex + (2 * (lowPrecision ? 1 : 2)), lowPrecision),
                (float) DataConverter.FromData(typeof(float), value, startIndex + (4 * (lowPrecision ? 1 : 2)), lowPrecision)
            );
            bytesRead = 6 * (lowPrecision ? 1 : 2);

            return result;
        }

        public static new byte[] GetData(Vector3 value, bool lowPrecision = false) {
            List<byte> bytes = new List<byte>();
            bytes.AddRange(FloatConverter.GetData(value.x, lowPrecision));
            bytes.AddRange(FloatConverter.GetData(value.y, lowPrecision));
            bytes.AddRange(FloatConverter.GetData(value.z, lowPrecision));

            return bytes.ToArray();
        }
    }
}
