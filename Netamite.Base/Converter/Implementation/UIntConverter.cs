using Netamite.Network;
using System;

namespace Netamite.Converter.Implementation {
    public class UIntConverter : DataConverterInterface<uint> {
        public static new uint FromBytes(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            uint result = BitConverter.ToUInt32(value, startIndex);
            bytesRead = 4;
            return result;
        }

        public static new byte[] GetBytes(uint value, bool lowPrecision = false) {
            return BitConverter.GetBytes(value);
        }

        public override void Write(BinaryNetStream stream, uint value, bool lowPrecision) {
            stream.WriteUInt(value);
        }

        public override uint Read(BinaryNetStream stream, bool lowPrecision) {
            return stream.ReadUInt();
        }
    }
}
