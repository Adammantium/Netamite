using Netamite.Datatype;
using System.Collections.Generic;

namespace Netamite.Converter.Implementation {
    public class QuaternionConverter : DataConverterInterface<Quaternion> {
        public static new Quaternion FromBytes(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            Quaternion result;
            if(lowPrecision) {
                byte maxIndex = value[startIndex];
                if(maxIndex >= 4 && maxIndex <= 7) {
                    result = Quaternion.Decompress(maxIndex);
                    bytesRead = 1;
                } else {
                    short[] data = new short[3] {
                        ShortConverter.FromBytes(value, out _, startIndex + 1),
                        ShortConverter.FromBytes(value, out _, startIndex + 3),
                        ShortConverter.FromBytes(value, out _, startIndex + 5)
                    };
                    result = Quaternion.Decompress(maxIndex, data);
                    bytesRead = 7;
                }
            } else {
                result = new Quaternion(
                    FloatConverter.FromBytes(value, out _, startIndex),
                    FloatConverter.FromBytes(value, out _, startIndex + 4),
                    FloatConverter.FromBytes(value, out _, startIndex + 8),
                    FloatConverter.FromBytes(value, out _, startIndex + 12)
                );
                bytesRead = 16;
            }

            return result;
        }

        public static new byte[] GetBytes(Quaternion value, bool lowPrecision = false) {
            if(lowPrecision) {
                return value.Compress();
            } else {
                List<byte> bytes = new List<byte>();
                bytes.AddRange(FloatConverter.GetBytes(value.x));
                bytes.AddRange(FloatConverter.GetBytes(value.y));
                bytes.AddRange(FloatConverter.GetBytes(value.z));
                bytes.AddRange(FloatConverter.GetBytes(value.w));
                return bytes.ToArray();
            }
        }
    }
}
