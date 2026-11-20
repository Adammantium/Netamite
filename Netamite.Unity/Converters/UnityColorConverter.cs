using Netamite.Converter;
using Netamite.Converter.Implementation;
using Netamite.Network;
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

        // Straight into the packet, without going through the Netamite Color and a byte[]
        public override void Write(BinaryNetStream stream, Color value, bool lowPrecision) {
            stream.WriteFloat(value.r, lowPrecision);
            stream.WriteFloat(value.g, lowPrecision);
            stream.WriteFloat(value.b, lowPrecision);
            stream.WriteFloat(value.a, lowPrecision);
        }

        public override Color Read(BinaryNetStream stream, bool lowPrecision) {
            float r = stream.ReadFloat(lowPrecision);
            float g = stream.ReadFloat(lowPrecision);
            float b = stream.ReadFloat(lowPrecision);
            float a = stream.ReadFloat(lowPrecision);
            return new Color(r, g, b, a);
        }
    }
}
