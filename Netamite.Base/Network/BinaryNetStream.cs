using Netamite.Converter;
using Netamite.Datatype;
using Netamite.Network.Packet;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace Netamite.Network {
    /// <summary>
    /// Growable byte buffer packets get written into and read from.
    /// Values are written straight into one array, nothing gets allocated per value.
    /// </summary>
    public class BinaryNetStream {
        // Free bytes kept in front of the data, so a length can be put in front of a packet without moving it
        private const int HEADROOM = 8;
        private const int INITIAL_CAPACITY = 256;
        // Streams that grew bigger than this are not kept around for reuse
        private const int MAX_POOLED_CAPACITY = 128 * 1024;

        private byte[] buffer;
        private int start;      // Index of the first byte of data in the buffer
        private int end;        // Index behind the last byte of data
        private int readPos;    // Relative to start
        private bool external;  // The buffer belongs to somebody else and must not be written to
        private bool disposed;

        public byte[] Data {
            get {
                if(buffer == null) return null;
                if(start == 0 && end == buffer.Length) return buffer;
                return ToArray();
            }
        }

        public BinaryNetStream() { }

        public BinaryNetStream(byte type) {
            WriteByte(type);
        }

        public BinaryNetStream(byte[] data) {
            SetBytes(data);
        }

        /// <summary>
        /// Reads from the given array. The array is not copied, so it must not be changed while reading from it.
        /// </summary>
        public void SetBytes(byte[] data) {
            SetBuffer(data, 0, data.Length, false);
        }

        /// <summary>
        /// Reads from a part of an array without copying it
        /// </summary>
        internal void SetBuffer(byte[] data, int offset, int count, bool resetPos = true) {
            buffer = data;
            start = offset;
            end = offset + count;
            external = true;
            if(resetPos) readPos = 0;
        }

        public void InsertInt(int value) {
            int at = ReserveFront(4);
            buffer[at]     = (byte) value;
            buffer[at + 1] = (byte) (value >> 8);
            buffer[at + 2] = (byte) (value >> 16);
            buffer[at + 3] = (byte) (value >> 24);
        }

        public byte[] ToArray() {
            int length = end - start;
            byte[] result = new byte[length];
            if(length > 0) Buffer.BlockCopy(buffer, start, result, 0, length);
            return result;
        }

        public int Length() {
            return end - start;
        }

        public int UnreadLength() {
            return Length() - readPos;
        }

        public void ResetPos() {
            readPos = 0;
        }

        public void Reset(bool shouldReset = true) {
            if(shouldReset) {
                Clear();
                return;
            }
            readPos -= 4;
        }

        /// <summary>
        /// Empties the stream but keeps its memory for the next use
        /// </summary>
        internal void Clear() {
            if(external) {
                buffer = null;
                external = false;
            }
            start = end = (buffer == null ? 0 : HEADROOM);
            readPos = 0;
        }

        // The array behind the stream and where the data sits in it, to hand the data to a socket without copying it
        internal byte[] RawBuffer { get { return buffer; } }
        internal int RawOffset { get { return start; } }
        internal int RawReadPosition { get { return start + readPos; } }
        internal int Capacity { get { return buffer == null ? 0 : buffer.Length; } }

        /// <summary>
        /// Cuts the stream back to the given length, used to drop a half written packet
        /// </summary>
        internal void SetLength(int length) {
            if(length < 0 || length > end - start) throw new ArgumentOutOfRangeException(nameof(length));
            end = start + length;
        }

        #region Memory handling
        private void EnsureWritable(int additional) {
            if(buffer == null) {
                buffer = new byte[Math.Max(INITIAL_CAPACITY, HEADROOM + additional)];
                start = end = HEADROOM;
                external = false;
                return;
            }
            if(!external && end + additional <= buffer.Length) return;

            int length = end - start;
            byte[] grown = new byte[Math.Max(INITIAL_CAPACITY, Math.Max(buffer.Length * 2, HEADROOM + length + additional))];
            if(length > 0) Buffer.BlockCopy(buffer, start, grown, HEADROOM, length);
            buffer = grown;
            start = HEADROOM;
            end = HEADROOM + length;
            external = false;
        }

        /// <summary>
        /// Makes room in front of the data and returns the index the new bytes have to be written to
        /// </summary>
        private int ReserveFront(int count) {
            if(buffer == null || external || start < count) {
                int length = end - start;
                byte[] target = (buffer != null && !external && HEADROOM + count + length <= buffer.Length)
                              ? buffer
                              : new byte[Math.Max(INITIAL_CAPACITY, (HEADROOM + count + length) * 2)];
                if(length > 0) Buffer.BlockCopy(buffer, start, target, HEADROOM + count, length);
                buffer = target;
                start = HEADROOM + count;
                end = start + length;
                external = false;
            }
            start -= count;
            return start;
        }

        /// <summary>
        /// Moves the read position forward and returns the index the data starts at
        /// </summary>
        private int Take(int count) {
            if(count < 0 || count > end - start - readPos) {
                throw new Exception("Could not read " + count + " bytes, only " + (end - start - readPos) + " left!");
            }
            int at = start + readPos;
            readPos += count;
            return at;
        }

        internal void Skip(int count) {
            Take(count);
        }

        [ThreadStatic] private static BinaryNetStream pooledWriter;
        [ThreadStatic] private static BinaryNetStream pooledReader;

        /// <summary>
        /// Every thread keeps one stream for writing around, so serializing a packet doesn't need a new buffer every time.
        /// Has to be given back with ReturnWriter. Nested use on the same thread just gets a fresh stream.
        /// </summary>
        internal static BinaryNetStream RentWriter() {
            BinaryNetStream stream = pooledWriter;
            if(stream == null) return new BinaryNetStream();
            pooledWriter = null;
            return stream;
        }

        internal static void ReturnWriter(BinaryNetStream stream) {
            if(stream.Capacity > MAX_POOLED_CAPACITY) return;
            stream.Clear();
            pooledWriter = stream;
        }

        internal static BinaryNetStream RentReader() {
            BinaryNetStream stream = pooledReader;
            if(stream == null) return new BinaryNetStream();
            pooledReader = null;
            return stream;
        }

        internal static void ReturnReader(BinaryNetStream stream) {
            stream.Clear(); // Lets go of the array that was read from
            pooledReader = stream;
        }
        #endregion

        #region Write to the Buffer
        /// <summary>
        /// Puts the length of everything written so far in front of it
        /// </summary>
        public void WriteLength() {
            int length = end - start;
            int at = ReserveFront(2);
            buffer[at]     = (byte) length;
            buffer[at + 1] = (byte) (length >> 8);
        }

        /// <summary>
        /// Overwrites two bytes that have already been written, position is counted from the start of the stream
        /// </summary>
        internal void PatchUShort(int position, ushort value) {
            buffer[start + position]     = (byte) value;
            buffer[start + position + 1] = (byte) (value >> 8);
        }

        public void WriteByte(byte value) {
            EnsureWritable(1);
            buffer[end++] = value;
        }

        public void WriteBool(bool value) {
            EnsureWritable(1);
            buffer[end++] = value ? (byte) 1 : (byte) 0;
        }

        public void WriteUShort(ushort value) {
            EnsureWritable(2);
            buffer[end]     = (byte) value;
            buffer[end + 1] = (byte) (value >> 8);
            end += 2;
        }

        public void WriteShort(short value) {
            WriteUShort((ushort) value);
        }

        public void WriteChar(char value) {
            WriteUShort(value);
        }

        public void WriteUInt(uint value) {
            EnsureWritable(4);
            buffer[end]     = (byte) value;
            buffer[end + 1] = (byte) (value >> 8);
            buffer[end + 2] = (byte) (value >> 16);
            buffer[end + 3] = (byte) (value >> 24);
            end += 4;
        }

        public void WriteInt(int value) {
            WriteUInt((uint) value);
        }

        public void WriteULong(ulong value) {
            EnsureWritable(8);
            uint low  = (uint) value;
            uint high = (uint) (value >> 32);
            buffer[end]     = (byte) low;
            buffer[end + 1] = (byte) (low >> 8);
            buffer[end + 2] = (byte) (low >> 16);
            buffer[end + 3] = (byte) (low >> 24);
            buffer[end + 4] = (byte) high;
            buffer[end + 5] = (byte) (high >> 8);
            buffer[end + 6] = (byte) (high >> 16);
            buffer[end + 7] = (byte) (high >> 24);
            end += 8;
        }

        public void WriteLong(long value) {
            WriteULong((ulong) value);
        }

        /// <summary>
        /// Low precision floats are sent as halfs (2 bytes instead of 4)
        /// </summary>
        public unsafe void WriteFloat(float value, bool lowPrecision = false) {
            if(lowPrecision) {
                WriteUShort(Half.GetBits(new Half(value)));
            } else {
                WriteUInt(*(uint*) &value);
            }
        }

        /// <summary>
        /// Low precision doubles are sent as floats (4 bytes instead of 8)
        /// </summary>
        public void WriteDouble(double value, bool lowPrecision = false) {
            if(lowPrecision) {
                WriteFloat((float) value);
            } else {
                WriteLong(BitConverter.DoubleToInt64Bits(value));
            }
        }

        /// <summary>
        /// Length (4 bytes) followed by the UTF8 bytes, null is sent as an empty string
        /// </summary>
        public void WriteString(string value) {
            if(string.IsNullOrEmpty(value)) {
                WriteInt(0);
                return;
            }

            EnsureWritable(4 + Encoding.UTF8.GetMaxByteCount(value.Length));
            int count = Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, end + 4);
            WriteInt(count);
            end += count;
        }

        /// <summary>
        /// Copies raw bytes into the stream, without a length in front
        /// </summary>
        public void WriteBuffer(byte[] data, int offset, int count) {
            if(count <= 0) return;
            EnsureWritable(count);
            Buffer.BlockCopy(data, offset, buffer, end, count);
            end += count;
        }

        public void WriteArray(object[] val, bool lowPrecision = false) {
            if(val == null) {
                WriteUShort(0);
                return;
            }

            WriteUShort((ushort) val.Length);
            for(int i = 0; i < val.Length; i++) {
                Write(val[i], lowPrecision);
            }
        }

        /// <summary>
        /// Same wire format as WriteArray with byte elements (ushort count, one byte each), but copies the array in one go
        /// </summary>
        public void WriteBytes(byte[] val) {
            if(val == null) {
                WriteUShort(0);
                return;
            }

            WriteUShort((ushort) val.Length);
            WriteBuffer(val, 0, val.Length);
        }

        public void WriteRawFallback(object val) {
            using(var stream = new MemoryStream()) {
                var formatter = new BinaryFormatter();
                formatter.Serialize(stream, val);
                stream.Flush();

                byte[] bytes = stream.ToArray();
                WriteBuffer(bytes, 0, bytes.Length);
            }
        }

        /// <summary>
        /// Writes all [SyncedVar] members of the object, one after another
        /// </summary>
        public void WriteRaw(object obj) {
            ObjectSerializer.Get(obj.GetType()).Write(this, obj);
        }

        /// <summary>
        /// Writes any value. Uses the DataConverter of its type, types without one get written member by member.
        /// </summary>
        public void Write(object val, bool lowPrecision = false) {
            if(val == null) throw new ArgumentNullException(nameof(val), "Can't write null, the receiver would not know what to read.");

            Type type = val.GetType();
            DataConverterBase converter = DataConverter.Find(type);
            if(converter != null) {
                converter.WriteBoxed(this, val, lowPrecision);
            } else {
                ObjectSerializer.Get(type).Write(this, val);
            }
        }
        #endregion

        #region Read from buffer
        public byte ReadByte(bool moveReadPos = true) {
            if(end - start > readPos) {
                byte result = buffer[start + readPos];
                if(moveReadPos) {
                    readPos++;
                }
                return result;
            }
            throw new Exception("Could not read value of type 'byte'!");
        }

        public bool ReadBool() {
            return buffer[Take(1)] != 0;
        }

        public ushort ReadUShort() {
            int at = Take(2);
            return (ushort) (buffer[at] | (buffer[at + 1] << 8));
        }

        public short ReadShort() {
            return (short) ReadUShort();
        }

        public char ReadChar() {
            return (char) ReadUShort();
        }

        public uint ReadUInt() {
            int at = Take(4);
            return (uint) (buffer[at] | (buffer[at + 1] << 8) | (buffer[at + 2] << 16) | (buffer[at + 3] << 24));
        }

        public int ReadInt() {
            return (int) ReadUInt();
        }

        public ulong ReadULong() {
            int at = Take(8);
            uint low  = (uint) (buffer[at]     | (buffer[at + 1] << 8) | (buffer[at + 2] << 16) | (buffer[at + 3] << 24));
            uint high = (uint) (buffer[at + 4] | (buffer[at + 5] << 8) | (buffer[at + 6] << 16) | (buffer[at + 7] << 24));
            return ((ulong) high << 32) | low;
        }

        public long ReadLong() {
            return (long) ReadULong();
        }

        public unsafe float ReadFloat(bool lowPrecision = false) {
            if(lowPrecision) {
                return (float) Half.ToHalf(ReadUShort());
            }
            uint bits = ReadUInt();
            return *(float*) &bits;
        }

        public double ReadDouble(bool lowPrecision = false) {
            if(lowPrecision) {
                return (double) ReadFloat();
            }
            return BitConverter.Int64BitsToDouble(ReadLong());
        }

        public string ReadString() {
            int count = ReadInt();
            if(count <= 0) return "";

            int at = Take(count);
            return Encoding.UTF8.GetString(buffer, at, count);
        }

        public object[] ReadArray(Type type, bool lowPrecision = false, bool moveReadPos = true) {
            int startPos = readPos;
            ushort length = ReadUShort();

            // Never trust the length for the allocation, a 3 byte packet could otherwise claim 65535 elements
            object[] values = new object[Math.Min((int) length, Math.Max(UnreadLength(), 16))];
            for(int i = 0; i < length; i++) {
                if(i == values.Length) Array.Resize(ref values, Math.Min((int) length, values.Length * 2));
                values[i] = Read(type, lowPrecision);
            }

            if(!moveReadPos) readPos = startPos;
            return values;
        }

        /// <summary>
        /// Counterpart of WriteBytes, reads data written by WriteArray with byte elements as well
        /// </summary>
        public byte[] ReadBytes(bool moveReadPos = true) {
            int startPos = readPos;
            if(UnreadLength() < 2) throw new Exception("Could not read value of type 'byte[]'!");
            int length = ReadUShort();
            if(UnreadLength() < length) {
                readPos = startPos;
                throw new Exception("Could not read value of type 'byte[]'!");
            }

            byte[] result = new byte[length];
            Buffer.BlockCopy(buffer, Take(length), result, 0, length);

            if(!moveReadPos) readPos = startPos;
            return result;
        }

        public object ReadRawFallback() {
            using(var stream = new MemoryStream(buffer, start, end - start)) {
                var formatter = new BinaryFormatter();
                stream.Seek(readPos, SeekOrigin.Begin);

                object val = formatter.Deserialize(stream);

                readPos = (int) stream.Position;

                return val;
            }
        }

        /// <summary>
        /// Creates an object of the type and reads all its [SyncedVar] members
        /// </summary>
        public object ReadRaw(Type type) {
            return ObjectSerializer.Get(type).ReadNew(this);
        }

        /// <summary>
        /// Reads any value. Uses the DataConverter of the type, types without one get read member by member.
        /// </summary>
        public object Read(Type type, bool lowPrecision = false, bool moveReadPos = true) {
            int startPos = readPos;

            object val;
            DataConverterBase converter = DataConverter.Find(type);
            if(converter != null) {
                val = converter.ReadBoxed(this, lowPrecision);
            } else {
                val = ObjectSerializer.Get(type).ReadNew(this);
            }

            if(!moveReadPos) readPos = startPos;
            return val;
        }
        #endregion

        protected virtual void Dispose(bool disposing) {
            if(!disposed) {
                if(disposing) {
                    buffer = null;
                    start = end = 0;
                    readPos = 0;
                    external = false;
                }
                disposed = true;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
