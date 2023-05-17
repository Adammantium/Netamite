using Netamite.Converter.Implementation;
using Netamite.Converter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    }
}
