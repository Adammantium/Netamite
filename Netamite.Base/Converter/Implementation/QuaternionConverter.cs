using Netamite.Network;
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

        public override void Write(BinaryNetStream stream, Quaternion value, bool lowPrecision) {
            WriteQuaternion(stream, value.x, value.y, value.z, value.w, lowPrecision);
        }

        public override Quaternion Read(BinaryNetStream stream, bool lowPrecision) {
            float x, y, z, w;
            ReadQuaternion(stream, out x, out y, out z, out w, lowPrecision);
            return new Quaternion(x, y, z, w);
        }

        /// <summary>
        /// Writes a quaternion given by its components, for converters of other quaternion types (like the one from Unity).
        /// Low precision uses the smallest three compression: 7 bytes instead of 16, or just 1 byte if one component is 1.
        /// </summary>
        public static void WriteQuaternion(BinaryNetStream stream, float x, float y, float z, float w, bool lowPrecision) {
            if(lowPrecision) {
                short a, b, c;
                byte header = Quaternion.Compress(x, y, z, w, out a, out b, out c);
                stream.WriteByte(header);
                if(header < 4) {
                    stream.WriteShort(a);
                    stream.WriteShort(b);
                    stream.WriteShort(c);
                }
            } else {
                stream.WriteFloat(x);
                stream.WriteFloat(y);
                stream.WriteFloat(z);
                stream.WriteFloat(w);
            }
        }

        /// <summary>
        /// Counterpart of WriteQuaternion
        /// </summary>
        public static void ReadQuaternion(BinaryNetStream stream, out float x, out float y, out float z, out float w, bool lowPrecision) {
            if(lowPrecision) {
                byte maxIndex = stream.ReadByte();
                if(maxIndex >= 4 && maxIndex <= 7) {
                    Quaternion.Decompress(maxIndex, 0, 0, 0, out x, out y, out z, out w);
                } else {
                    short a = stream.ReadShort();
                    short b = stream.ReadShort();
                    short c = stream.ReadShort();
                    Quaternion.Decompress(maxIndex, a, b, c, out x, out y, out z, out w);
                }
            } else {
                x = stream.ReadFloat();
                y = stream.ReadFloat();
                z = stream.ReadFloat();
                w = stream.ReadFloat();
            }
        }
    }
}
