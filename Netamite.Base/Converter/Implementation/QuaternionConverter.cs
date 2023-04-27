using Netamite.Datatype;
using System;
using System.Collections.Generic;

namespace Netamite.Converter.Implementation {
    internal class QuaternionConverter : DataConverterInterface<Quaternion> {
        public static new Quaternion FromData(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            Quaternion result;
            if(lowPrecision) {
                byte maxIndex = value[startIndex];
                if(maxIndex >= 4 && maxIndex <= 7) {
                    result = Quaternion.Decompress(maxIndex);
                    bytesRead = 1;
                } else {
                    short[] data = new short[3] {
                        (short) DataConverter.FromData(typeof(short), value, startIndex + 1),
                        (short) DataConverter.FromData(typeof(short), value, startIndex + 3),
                        (short) DataConverter.FromData(typeof(short), value, startIndex + 5)
                    };
                    result = Quaternion.Decompress(maxIndex, data);
                    bytesRead = 7;
                }
            } else {
                result = new Quaternion(
                    (float) DataConverter.FromData(typeof(float), value, startIndex), 
                    (float) DataConverter.FromData(typeof(float), value, startIndex + 4),
                    (float) DataConverter.FromData(typeof(float), value, startIndex + 8),
                    (float) DataConverter.FromData(typeof(float), value, startIndex + 12)
                );
                bytesRead = 16;
            }

            return result;
        }

        public static new byte[] GetData(Quaternion value, bool lowPrecision = false) {
            if(lowPrecision) {
                return value.Compress();
            } else {
                List<byte> bytes = new List<byte>();
                bytes.AddRange(FloatConverter.GetData(value.x));
                bytes.AddRange(FloatConverter.GetData(value.y));
                bytes.AddRange(FloatConverter.GetData(value.z));
                bytes.AddRange(FloatConverter.GetData(value.w));
                return bytes.ToArray();
            }
        }
    }
}
