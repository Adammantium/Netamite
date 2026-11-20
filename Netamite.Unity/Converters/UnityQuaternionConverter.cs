using Netamite.Converter;
using Netamite.Converter.Implementation;
using Netamite.Network;
using UnityEngine;

namespace Netamite.Unity.Converters {
    internal class UnityQuaternionConverter : DataConverterInterface<Quaternion> {
        public static new Quaternion FromBytes(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            return FromBuiltin(QuaternionConverter.FromBytes(value, out bytesRead, startIndex, lowPrecision));
        }

        public static new byte[] GetBytes(Quaternion value, bool lowPrecision = false) {
            return QuaternionConverter.GetBytes(ToBuiltin(value), lowPrecision);
        }

        public static Datatype.Quaternion ToBuiltin(Quaternion n) {
            return new Datatype.Quaternion(n.x, n.y, n.z, n.w);
        }

        public static Quaternion FromBuiltin(Datatype.Quaternion o) {
            return new Quaternion(o.x, o.y, o.z, o.w);
        }

        // Straight into the packet, without going through the Netamite Quaternion and a byte[]
        public override void Write(BinaryNetStream stream, Quaternion value, bool lowPrecision) {
            QuaternionConverter.WriteQuaternion(stream, value.x, value.y, value.z, value.w, lowPrecision);
        }

        public override Quaternion Read(BinaryNetStream stream, bool lowPrecision) {
            float x, y, z, w;
            QuaternionConverter.ReadQuaternion(stream, out x, out y, out z, out w, lowPrecision);
            return new Quaternion(x, y, z, w);
        }
    }
}
