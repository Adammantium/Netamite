using System;
using System.Collections.Generic;

namespace Netamite.Datatype {
    [Serializable]
    public class Quaternion {
        public float x, y, z, w;

        private static float Rad2Deg = (float) (180 / Math.PI);
        private static float Deg2Rad = (float) (Math.PI / 180);

        public static Quaternion identity {
            get { return new Quaternion(0f, 0f, 0f, 1f); }
        }

        public float this[int i] {
            get { return (i == 0 ? x : (i == 1 ? y : (i == 2 ? z : w))); }
        }

        public Quaternion() {

        }

        public Quaternion(float x, float y, float z, float w) {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public override string ToString() {
            return $"({x}; {y}; {z}; {w})";
        }

        public static bool operator ==(Quaternion a, Quaternion b) {
            return a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;
        }
        public static bool operator !=(Quaternion a, Quaternion b) {
            return !(a == b);
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

        /*
        public static Quaternion FromEuler(Vector3 vec) {
            return FromEuler(vec.y, vec.x, vec.z);
        }

        public static Quaternion FromEuler(float yaw, float pitch, float roll) {
            yaw   *= Deg2Rad;
            pitch *= Deg2Rad;
            roll  *= Deg2Rad;
            float rollOver2 = roll * 0.5f;
            float sinRollOver2 = (float)Math.Sin((double)rollOver2);
            float cosRollOver2 = (float)Math.Cos((double)rollOver2);
            float pitchOver2 = pitch * 0.5f;
            float sinPitchOver2 = (float)Math.Sin((double)pitchOver2);
            float cosPitchOver2 = (float)Math.Cos((double)pitchOver2);
            float yawOver2 = yaw * 0.5f;
            float sinYawOver2 = (float)Math.Sin((double)yawOver2);
            float cosYawOver2 = (float)Math.Cos((double)yawOver2);
            Quaternion result = new Quaternion();
            result.w = cosYawOver2 * cosPitchOver2 * cosRollOver2 + sinYawOver2 * sinPitchOver2 * sinRollOver2;
            result.x = cosYawOver2 * sinPitchOver2 * cosRollOver2 + sinYawOver2 * cosPitchOver2 * sinRollOver2;
            result.y = sinYawOver2 * cosPitchOver2 * cosRollOver2 - cosYawOver2 * sinPitchOver2 * sinRollOver2;
            result.z = cosYawOver2 * cosPitchOver2 * sinRollOver2 - sinYawOver2 * sinPitchOver2 * cosRollOver2;

            return result;
        }


        public Vector3 ToEuler() {
            float sqw = w * w;
            float sqx = x * x;
            float sqy = y * y;
            float sqz = z * z;
            float unit = sqx + sqy + sqz + sqw; // if normalised is one, otherwise is correction factor
            float test = x * w - y * z;
            Vector3 v = new Vector3();

            if(test > 0.4995f * unit) { // singularity at north pole
                v.y = (float) (2f * Math.Atan2(y, x));
                v.x = (float) (Math.PI / 2);
                v.z = 0;
                (v * Rad2Deg).NormalizeAngles();
                return v;
            }

            if(test < -0.4995f * unit) { // singularity at south pole
                v.y = (float) (-2f * Math.Atan2(y, x));
                v.x = (float) (-Math.PI / 2);
                v.z = 0;
                (v * Rad2Deg).NormalizeAngles();
                return v;
            }

            Quaternion q = new Quaternion(w, z, x, y);
            v.y = (float)Math.Atan2(2f * q.x * q.w + 2f * q.y * q.z, 1 - 2f * (q.z * q.z + q.w * q.w));     // Yaw
            v.x = (float)Math.Asin(2f * (q.x * q.z - q.w * q.y));                                           // Pitch
            v.z = (float)Math.Atan2(2f * q.x * q.y + 2f * q.z * q.w, 1 - 2f * (q.y * q.y + q.z * q.z));     // Roll

            (v * Rad2Deg).NormalizeAngles();
            return v;
        }
        */
    }
}