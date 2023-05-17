using Netamite.Datatype;
using System.Collections.Generic;

namespace Netamite.Converter.Implementation {
    public class Vector3Converter : DataConverterInterface<Vector3> {
        public static new Vector3 FromBytes(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            Vector3 result = new Vector3(
                FloatConverter.FromBytes(value, out _, startIndex                               , lowPrecision), 
                FloatConverter.FromBytes(value, out _, startIndex + (2 * (lowPrecision ? 1 : 2)), lowPrecision),
                FloatConverter.FromBytes(value, out _, startIndex + (4 * (lowPrecision ? 1 : 2)), lowPrecision)
            );
            bytesRead = 6 * (lowPrecision ? 1 : 2);

            return result;
        }

        public static new byte[] GetBytes(Vector3 value, bool lowPrecision = false) {
            List<byte> bytes = new List<byte>();
            bytes.AddRange(FloatConverter.GetBytes(value.x, lowPrecision));
            bytes.AddRange(FloatConverter.GetBytes(value.y, lowPrecision));
            bytes.AddRange(FloatConverter.GetBytes(value.z, lowPrecision));

            return bytes.ToArray();
        }
    }
}
