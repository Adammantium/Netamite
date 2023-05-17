using Netamite.Converter;
using Netamite.Converter.Implementation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    }
}
