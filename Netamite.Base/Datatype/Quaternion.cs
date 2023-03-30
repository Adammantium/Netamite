using System;
using System.Collections.Generic;

namespace Netamite.Datatype {
    public class Quaternion {
        public float x, y, z, w;

        public float this[int i] {
            get { return (i == 0 ? x : (i == 1 ? y : (i == 2 ? z : w))); }
        }

        public Quaternion(float x, float y, float z, float w) {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        /// <summary>
        /// Used when compressing float values, where the decimal portion of the floating point value
        /// is multiplied by this number prior to storing the result in an Int16. Doing this allows 
        /// us to retain five decimal places, which for many purposes is more than adequate.
        /// </summary>
        private const float FLOAT_PRECISION_MULT = 10000f;

        public byte[] Compress() {
            byte maxIndex = 0;
            float maxValue = float.MinValue;
            float sign = 1f;

            // Determine the index of the largest (absolute value) element in the Quaternion.
            // We will transmit only the three smallest elements, and reconstruct the largest
            // element during decoding. 
            for(int i = 0; i < 4; i++) {
                float element = this[i];
                float abs     = Math.Abs(element);
                if(abs > maxValue) {
                    // We don't need to explicitly transmit the sign bit of the omitted element because you 
                    // can make the omitted element always positive by negating the entire quaternion if 
                    // the omitted element is negative (in quaternion space (x,y,z,w) and (-x,-y,-z,-w) 
                    // represent the same rotation.), but we need to keep track of the sign for use below.
                    sign = (element < 0) ? -1 : 1;

                    // Keep track of the index of the largest element
                    maxIndex = (byte) i;
                    maxValue = abs;
                }
            }

            // If the maximum value is approximately 1f (such as Quaternion.identity [0,0,0,1]), then we can 
            // reduce storage even further due to the fact that all other fields must be 0f by definition, so 
            // we only need to send the index of the largest field.
            if(maxValue == 1f) {
                // Again, don't need to transmit the sign since in quaternion space (x,y,z,w) and (-x,-y,-z,-w) 
                // represent the same rotation. We only need to send the index of the single element whose value
                // is 1f in order to recreate an equivalent rotation on the receiver.
                return new byte[] { (byte)(maxIndex + 4) };
            }

            short a;
            short b;
            short c;

            // We multiply the value of each element by QUAT_PRECISION_MULT before converting to 16-bit integer 
            // in order to maintain precision. This is necessary since by definition each of the three smallest 
            // elements are less than 1.0, and the conversion to 16-bit integer would otherwise truncate everything 
            // to the right of the decimal place. This allows us to keep five decimal places.

            if(maxIndex == 0) {
                a = (short)(y * sign * FLOAT_PRECISION_MULT);
                b = (short)(z * sign * FLOAT_PRECISION_MULT);
                c = (short)(w * sign * FLOAT_PRECISION_MULT);
            } else if(maxIndex == 1) {
                a = (short)(x * sign * FLOAT_PRECISION_MULT);
                b = (short)(z * sign * FLOAT_PRECISION_MULT);
                c = (short)(w * sign * FLOAT_PRECISION_MULT);
            } else if(maxIndex == 2) {
                a = (short)(x * sign * FLOAT_PRECISION_MULT);
                b = (short)(y * sign * FLOAT_PRECISION_MULT);
                c = (short)(w * sign * FLOAT_PRECISION_MULT);
            } else {
                a = (short)(x * sign * FLOAT_PRECISION_MULT);
                b = (short)(y * sign * FLOAT_PRECISION_MULT);
                c = (short)(z * sign * FLOAT_PRECISION_MULT);
            }

            List<byte> list = new List<byte>();
            list.Add(maxIndex);
            list.AddRange(BitConverter.GetBytes(a));
            list.AddRange(BitConverter.GetBytes(b));
            list.AddRange(BitConverter.GetBytes(c));

            return list.ToArray();
        }

        public static Quaternion Decompress(byte maxIndex, short[] data = null) {
            // Values between 4 and 7 indicate that only the index of the single field whose value is 1f was
            // sent, and (maxIndex - 4) is the correct index for that field.
            if(maxIndex >= 4 && maxIndex <= 7) {
                float x = (maxIndex == 4) ? 1f : 0f;
                float y = (maxIndex == 5) ? 1f : 0f;
                float z = (maxIndex == 6) ? 1f : 0f;
                float w = (maxIndex == 7) ? 1f : 0f;

                return new Quaternion(x, y, z, w);
            }

            if(data == null) {
                throw new ArgumentNullException("Quaternion couldn't be decompressed, no data specified!");
            }

            if(data.Length < 3) {
                throw new ArgumentNullException($"Quaternion couldn't be decompressed, not enough data specified! Required: 3 / Provided: {data.Length}");
            }

            // Read the other three fields and derive the value of the omitted field
            float a = (float) data[0] / FLOAT_PRECISION_MULT;
            float b = (float) data[1] / FLOAT_PRECISION_MULT;
            float c = (float) data[2] / FLOAT_PRECISION_MULT;
            float d = (float) Math.Sqrt(1f - (a * a + b * b + c * c));

            if(maxIndex == 0)
                return new Quaternion(d, a, b, c);
            else if(maxIndex == 1)
                return new Quaternion(a, d, b, c);
            else if(maxIndex == 2)
                return new Quaternion(a, b, d, c);

            return new Quaternion(a, b, c, d);
        }
    }
}