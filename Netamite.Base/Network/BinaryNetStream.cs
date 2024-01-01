using Netamite.Converter;
using Netamite.Extension;
using Netamite.Network.Packet.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
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
            MemberInfo[] myMembers = obj.GetType().GetMembers();

            for(int i = 0; i < myMembers.Length; i++) {
                MemberInfo memberInfo = myMembers[i];

                object[] atts = memberInfo.GetCustomAttributes(typeof(SyncedVar), true);

                if(atts.Length > 0) {
                    Type t = memberInfo.GetUnderlyingType();

                    if(t.IsArray) {
                        System.Collections.IList val = (System.Collections.IList) memberInfo.GetValue(obj);

                        if(val == null) {
                            WriteArray(null);
                        } else {
                            object[] array = new object[val.Count];
                            val.CopyTo(array, 0);

                            WriteArray(array, (atts[0] as SyncedVar).LowPrecision);
                        }
                    } else {
                        object val = memberInfo.GetValue(obj);

                        Write(val, (atts[0] as SyncedVar).LowPrecision);
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
            MemberInfo[] myMembers = type.GetMembers();
            object obj = Activator.CreateInstance(type);

            for(int i = 0; i < myMembers.Length; i++) {
                MemberInfo memberInfo = myMembers[i];

                object[] atts = memberInfo.GetCustomAttributes(typeof(SyncedVar), true);

                if(atts.Length > 0) {
                    Type t = memberInfo.GetUnderlyingType();

                    //try {
                    if(t.IsArray) {
                        object[] val = ReadArray(t.GetElementType(), (atts[0] as SyncedVar).LowPrecision);

                        if(val.Length > 0 && val[0] != null) {
                            Array filledArray = Array.CreateInstance(val[0].GetType(), val.Length);
                            Array.Copy(val, filledArray, val.Length);

                            memberInfo.SetValue(obj, filledArray);
                        }
                    } else {
                        object val = Read(t, (atts[0] as SyncedVar).LowPrecision);

                        memberInfo.SetValue(obj, val);
                    }
                }
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
