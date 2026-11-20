using Netamite.Network;
namespace Netamite.Converter.Implementation {
    public class ByteConverter : DataConverterInterface<byte> {
        public static new byte FromBytes(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            byte result = value[startIndex];
            bytesRead = 1;
            return result;
        }

        public static new byte[] GetBytes(byte value, bool lowPrecision = false) {
            return new byte[1] { value };
        }

        public override void Write(BinaryNetStream stream, byte value, bool lowPrecision) {
            stream.WriteByte(value);
        }

        public override byte Read(BinaryNetStream stream, bool lowPrecision) {
            return stream.ReadByte();
        }
    }
}
