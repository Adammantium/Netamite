using Netamite.Converter;
using Netamite.Converter.Implementation;
using Netamite.Network;
using UnityEngine;

namespace Netamite.Unity.Converters {
    internal class UnityVector3Converter : DataConverterInterface<Vector3> {
        public static new Vector3 FromBytes(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            return FromBuiltin(Vector3Converter.FromBytes(value, out bytesRead, startIndex, lowPrecision));
        }

        public static new byte[] GetBytes(Vector3 value, bool lowPrecision = false) {
            return Vector3Converter.GetBytes(ToBuiltin(value), lowPrecision);
        }

        public static Datatype.Vector3 ToBuiltin(Vector3 n) {
            return new Datatype.Vector3(n.x, n.y, n.z);
        }

        public static Vector3 FromBuiltin(Datatype.Vector3 o) {
            return new Vector3(o.x, o.y, o.z);
        }

        // Straight into the packet, without going through the Netamite Vector3 and a byte[]
        public override void Write(BinaryNetStream stream, Vector3 value, bool lowPrecision) {
            stream.WriteFloat(value.x, lowPrecision);
            stream.WriteFloat(value.y, lowPrecision);
            stream.WriteFloat(value.z, lowPrecision);
        }

        public override Vector3 Read(BinaryNetStream stream, bool lowPrecision) {
            float x = stream.ReadFloat(lowPrecision);
            float y = stream.ReadFloat(lowPrecision);
            float z = stream.ReadFloat(lowPrecision);
            return new Vector3(x, y, z);
        }
    }
}
