using Netamite.Network;
using System;

namespace Netamite.Converter.Implementation {
    public class CharConverter : DataConverterInterface<char> {
        public static new char FromBytes(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            char result = BitConverter.ToChar(value, startIndex);
            bytesRead = 2; // GetBytes writes both bytes of the char
            return result;
        }

        public static new byte[] GetBytes(char value, bool lowPrecision = false) {
            return BitConverter.GetBytes(value);
        }

        public override void Write(BinaryNetStream stream, char value, bool lowPrecision) {
            stream.WriteChar(value);
        }

        public override char Read(BinaryNetStream stream, bool lowPrecision) {
            return stream.ReadChar();
        }
    }
}
