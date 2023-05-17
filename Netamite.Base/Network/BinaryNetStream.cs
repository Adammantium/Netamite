using Netamite.Converter;
using Netamite.Datatype;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace Netamite.Network {
    public class BinaryNetStream {
        private List<byte> buffer = new List<byte>();

        private byte[] readableBuffer;
        private int readPos;
        private bool disposed;

        public byte[] Data {
            get { return readableBuffer; }
        }

        public BinaryNetStream() {
            readPos = 0;
        }

        public BinaryNetStream(byte type) {
            readPos = 0;
            Write(type);
        }

        public BinaryNetStream(byte[] data) {
            readPos = 0;
            SetBytes(data);
        }

        public void SetBytes(byte[] data) {
            buffer.Clear();
            buffer.AddRange(data);
            readableBuffer = buffer.ToArray();
        }

        public void InsertInt(int value) {
            buffer.InsertRange(0, BitConverter.GetBytes(value));
        }

        public byte[] ToArray() {
            readableBuffer = buffer.ToArray();
            return readableBuffer;
        }

        public int Length() {
            return buffer.Count;
        }

        public int UnreadLength() {
            return Length() - readPos;
        }

        public void ResetPos() {
            readPos = 0;
        }

        public void Reset(bool shouldReset = true) {
            if(shouldReset) {
                buffer.Clear();
                readableBuffer = null;
                readPos = 0;
                return;
            }
            readPos -= 4;
        }

        #region Write to the Buffer
        public void WriteLength() {
            buffer.InsertRange(0, BitConverter.GetBytes((ushort) buffer.Count));
        }

        public void WriteArray(object[] val, bool lowPrecision = false) {
            if(val == null) {
                Write((byte) 0);
                return;
            }

            Type type = val.GetType();

            if(type.IsArray) {
                Write((byte) val.Length);

                foreach(object v in val) {
                    Write(v, lowPrecision);
                }
            }
        }

        public void WriteRaw(object val) {
            using(var stream = new MemoryStream()) {
                var formatter = new BinaryFormatter();
                formatter.Serialize(stream, val);
                stream.Flush();
                stream.Position = 0;

                byte[] bytes = stream.ToArray();
                buffer.AddRange(bytes);
            }
        }

        public void Write(object val, bool lowPrecision = false) {
            try {
                byte[] bytes = DataConverter.GetBytes(val, lowPrecision);
                if(bytes != null) {
                    buffer.AddRange(bytes);
                    return;
                }
            }catch(MissingDataConverterException) { } // Ignore it and just write it raw
            
            WriteRaw(val);
        }
        #endregion

        #region Read from buffer
        public byte ReadByte(bool moveReadPos = true) {
            if(buffer.Count > readPos) {
                byte result = readableBuffer[readPos];
                if(moveReadPos) {
                    readPos++;
                }
                return result;
            }
            throw new Exception("Could not read value of type 'byte'!");
        }

        public object[] ReadArray(Type type, bool lowPrecision = false, bool moveReadPos = true) {
            byte length = ReadByte(moveReadPos);

            object[] values = new object[length];
            for(byte i = 0; i < length; i++) {
                values[i] = Read(type, lowPrecision, moveReadPos);
            }
            return values;
        }

        public T ReadRaw<T>() {
            using(var stream = new MemoryStream(readableBuffer)) {
                var formatter = new BinaryFormatter();
                stream.Seek(readPos, SeekOrigin.Begin);

                T val = (T) formatter.Deserialize(stream);

                readPos += (int) stream.Position;

                return val;
            }
        }

        public object Read(Type type, bool lowPrecision = false, bool moveReadPos = true) {
            try {
                int bytesRead = 0;
                object val = DataConverter.FromBytes(type, readableBuffer, out bytesRead, readPos, lowPrecision);
                if(val != null) {
                    if(moveReadPos) readPos += bytesRead;
                    return val;
                }
            } catch(MissingDataConverterException) { } // Ignore it and just read it raw

            return null;
            //return ReadRaw(val); // not working atm
        }
        #endregion

        protected virtual void Dispose(bool disposing) {
            if(!disposed) {
                if(disposing) {
                    buffer = null;
                    readableBuffer = null;
                    readPos = 0;
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
