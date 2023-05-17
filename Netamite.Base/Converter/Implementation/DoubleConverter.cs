using System;

namespace Netamite.Converter.Implementation {
    public class DoubleConverter : DataConverterInterface<double> {
        public static new double FromBytes(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            double val;
            if(lowPrecision) {
                float result = BitConverter.ToSingle(value, startIndex);

                val = (double) result;
                bytesRead = 4;
            } else {
                val = BitConverter.ToDouble(value, startIndex);
                bytesRead = 8;
            }
            return val;
        }

        public static new byte[] GetBytes(double value, bool lowPrecision = false) {
            if(lowPrecision) {
                return BitConverter.GetBytes((float) value);
            } else {
                return BitConverter.GetBytes(value);
            }
        }
    }
}
