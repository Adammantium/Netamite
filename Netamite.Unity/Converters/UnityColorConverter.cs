using Netamite.Converter;
using Netamite.Converter.Implementation;
using UnityEngine;

namespace Netamite.Unity.Converters {
    internal class UnityColorConverter : DataConverterInterface<Color> {
        public static new Color FromBytes(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            return FromBuiltin(ColorConverter.FromBytes(value, out bytesRead, startIndex, lowPrecision));
        }

        public static new byte[] GetBytes(Color value, bool lowPrecision = false) {
            return ColorConverter.GetBytes(ToBuiltin(value), lowPrecision);
        }

        public static Datatype.Color ToBuiltin(Color n) {
            return new Datatype.Color(n.r, n.g, n.b, n.a);
        }

        public static Color FromBuiltin(Datatype.Color o) {
            return new Color(o.r, o.g, o.b, o.a);
        }
    }
}
