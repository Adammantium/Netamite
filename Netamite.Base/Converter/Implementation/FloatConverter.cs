using Netamite.Datatype;
using System;

namespace Netamite.Converter.Implementation {
    public class FloatConverter : DataConverterInterface<float> {
        public static new float FromBytes(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            bytesRead = 0;
            float val;
            if(lowPrecision) {
                ushort result = BitConverter.ToUInt16(value, startIndex);

                val = (float) Half.ToHalf(result);
                bytesRead = 2;
            } else {
                val = BitConverter.ToSingle(value, startIndex);
                bytesRead = 4;
            }
            return val;
        }

        public static new byte[] GetBytes(float value, bool lowPrecision = false) {
            if(lowPrecision) {
                return Half.GetBytes(new Half(value));
            } else {
                return BitConverter.GetBytes(value);
            }
        }
    }
}
