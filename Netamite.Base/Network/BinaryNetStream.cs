using Netamite.Converter;
using Netamite.Extension;
using Netamite.Network.Packet;
using Netamite.Network.Packet.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;

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
                Write((ushort) 0);
                return;
            }

            Type type = val.GetType();

            if(type.IsArray) {
                Write((ushort) val.Length);

                foreach(object v in val) {
                    Write(v, lowPrecision);
                }
            }
        }

        /// <summary>
        /// Same wire format as WriteArray with byte elements (ushort count, one byte each), but copies the array
        /// in one go instead of boxing and converting every single byte, which produced ~200 bytes of garbage per byte
        /// </summary>
        public void WriteBytes(byte[] val) {
            if(val == null) {
                Write((ushort) 0);
                return;
            }

            Write((ushort) val.Length);
            buffer.AddRange(val);
        }

        public void WriteRawFallback(object val) {
            using(var stream = new MemoryStream()) {
                var formatter = new BinaryFormatter();
                formatter.Serialize(stream, val);
                stream.Flush();

                byte[] bytes = stream.ToArray();
                buffer.AddRange(bytes);
            }
        }

        public void WriteRaw(object obj) {
            SyncedMember[] myMembers = SyncedMemberCache.Get(obj.GetType());

            for(int i = 0; i < myMembers.Length; i++) {
                MemberInfo memberInfo = myMembers[i].Member;
                SyncedVar syncedVar = myMembers[i].Attribute;

                if(syncedVar != null) {
                    Type t = myMembers[i].Type;

                    if(t == typeof(byte[])) {
                        WriteBytes((byte[]) memberInfo.GetValue(obj));
                    } else if(t.IsArray) {
                        System.Collections.IList val = (System.Collections.IList) memberInfo.GetValue(obj);

                        if(val == null) {
                            WriteArray(null);
                        } else {
                            object[] array = new object[val.Count];
                            val.CopyTo(array, 0);

                            WriteArray(array, syncedVar.LowPrecision);
                        }
                    } else {
                        object val = memberInfo.GetValue(obj);

                        Write(val, syncedVar.LowPrecision);
                    }
                }
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
            ushort length = (ushort) Read(typeof(ushort), false, moveReadPos);

            object[] values = new object[length];
            for(ushort i = 0; i < length; i++) {
                values[i] = Read(type, lowPrecision, moveReadPos);
            }
            return values;
        }

        /// <summary>
        /// Counterpart of WriteBytes, reads data written by WriteArray with byte elements as well
        /// </summary>
        public byte[] ReadBytes(bool moveReadPos = true) {
            if(readPos + 2 > readableBuffer.Length) throw new Exception("Could not read value of type 'byte[]'!");
            int length = BitConverter.ToUInt16(readableBuffer, readPos);
            if(readPos + 2 + length > readableBuffer.Length) throw new Exception("Could not read value of type 'byte[]'!");

            byte[] result = new byte[length];
            Buffer.BlockCopy(readableBuffer, readPos + 2, result, 0, length);

            if(moveReadPos) readPos += 2 + length;
            return result;
        }

        public object ReadRawFallback() {
            using(var stream = new MemoryStream(readableBuffer)) {
                var formatter = new BinaryFormatter();
                stream.Seek(readPos, SeekOrigin.Begin);

                object val = formatter.Deserialize(stream);

                readPos = (int) stream.Position;

                return val;
            }
        }

        public object ReadRaw(Type type) {
            SyncedMember[] myMembers = SyncedMemberCache.Get(type);
            object obj = Activator.CreateInstance(type);

            bool readAnything = false;

            for(int i = 0; i < myMembers.Length; i++) {
                MemberInfo memberInfo = myMembers[i].Member;
                SyncedVar syncedVar = myMembers[i].Attribute;

                if(syncedVar != null) {
                    readAnything = true;
                    Type t = myMembers[i].Type;

                    //try {
                    if(t == typeof(byte[])) {
                        byte[] val = ReadBytes();
                        if(val.Length > 0) memberInfo.SetValue(obj, val); // Empty arrays stay unset, like ReadArray
                    } else if(t.IsArray) {
                        object[] val = ReadArray(t.GetElementType(), syncedVar.LowPrecision);

                        if(val.Length > 0 && val[0] != null) {
                            Array filledArray = Array.CreateInstance(val[0].GetType(), val.Length);
                            Array.Copy(val, filledArray, val.Length);

                            memberInfo.SetValue(obj, filledArray);
                        }
                    } else {
                        object val = Read(t, syncedVar.LowPrecision);

                        memberInfo.SetValue(obj, val);
                    }
                }
            }

            if(!readAnything) {
                // No [SyncedVar] members and no DataConverter for this type: reading it would silently
                // consume zero bytes and desync every field that follows. Fail loudly instead.
                throw new MissingDataConverterException(type);
            }

            return obj;
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

            return ReadRaw(type);
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
