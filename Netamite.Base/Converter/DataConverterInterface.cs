using Netamite.Network;
using Netamite.Network.Packet;
using System;
using System.Reflection;

namespace Netamite.Converter {
    /// <summary>
    /// What the serializer needs from a converter without knowing which type it converts.
    /// Converters are written by extending DataConverterInterface, not this class.
    /// </summary>
    public abstract class DataConverterBase {
        internal DataConverterBase() { }

        internal abstract Type ValueType { get; }

        internal abstract void WriteBoxed(BinaryNetStream stream, object value, bool lowPrecision);
        internal abstract object ReadBoxed(BinaryNetStream stream, bool lowPrecision);

        /// <summary>
        /// Builds the reader/writer for a field of the converted type (or of an enum based on it)
        /// </summary>
        internal abstract MemberCodec CreateMember(FieldInfo field, bool lowPrecision);

        /// <summary>
        /// Builds the reader/writer for a field that is an array of the converted type
        /// </summary>
        internal abstract MemberCodec CreateArrayMember(FieldInfo field, bool lowPrecision);
    }

    /// <summary>
    /// Base class of all converters. A converter turns one type into bytes and back.
    ///
    /// Override Write and Read, they put the value straight into the packet without creating any garbage.
    /// Converters that only have the static GetBytes / FromBytes methods keep working, they are just slower.
    /// </summary>
    public class DataConverterInterface<T> : DataConverterBase {
        /// <summary>
        /// Gets the binary data of the value
        /// </summary>
        /// <param name="value">The object you want to convert to binary</param>
        /// <param name="lowPrecision">If possible write the value with a low precision</param>
        /// <returns>Binary Data</returns>
        public static byte[] GetBytes(T value, bool lowPrecision = false) {
            return null;
        }

        public static T FromBytes(byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            bytesRead = 0;
            return default(T);
        }

        /// <summary>
        /// Writes the value into the stream
        /// </summary>
        /// <param name="stream">Stream of the packet that is being written</param>
        /// <param name="value">The value to write</param>
        /// <param name="lowPrecision">If possible write the value with a low precision</param>
        public virtual void Write(BinaryNetStream stream, T value, bool lowPrecision) {
            byte[] bytes = Legacy.getBytes(value, lowPrecision);
            if(bytes != null) {
                stream.WriteBuffer(bytes, 0, bytes.Length);
                return;
            }

            // The converter didn't want the value, in that case it gets written member by member
            if(value == null) throw new ArgumentNullException(nameof(value));
            ObjectSerializer.Get(value.GetType()).Write(stream, value);
        }

        /// <summary>
        /// Reads the value from the stream, has to read exactly what Write has written
        /// </summary>
        public virtual T Read(BinaryNetStream stream, bool lowPrecision) {
            int bytesRead;
            T result = Legacy.fromBytes(stream.RawBuffer, out bytesRead, stream.RawReadPosition, lowPrecision);
            if(isValueType || result != null) {
                stream.Skip(bytesRead);
                return result;
            }
            return (T) ObjectSerializer.Get(typeof(T)).ReadNew(stream);
        }

        #region Bridge to converters that only implement the static methods
        private static readonly bool isValueType = typeof(T).IsValueType;

        private delegate T FromBytesHandler(byte[] value, out int bytesRead, int startIndex, bool lowPrecision);

        private sealed class LegacyMethods {
            public Func<T, bool, byte[]> getBytes;
            public FromBytesHandler fromBytes;
        }

        // The class the static GetBytes / FromBytes methods are taken from, by default the converter itself
        internal Type legacySource;
        private LegacyMethods legacy;

        private LegacyMethods Legacy {
            get {
                LegacyMethods result = legacy;
                if(result != null) return result;

                Type source = legacySource ?? GetType();
                MethodInfo getBytes = source.GetMethod("GetBytes");
                MethodInfo fromBytes = source.GetMethod("FromBytes");
                if(getBytes == null || fromBytes == null) throw new MissingDataConverterException(typeof(T));

                result = new LegacyMethods();
                // A delegate is way faster than MethodInfo.Invoke, but needs the signature to match exactly
                try { result.getBytes = (Func<T, bool, byte[]>) Delegate.CreateDelegate(typeof(Func<T, bool, byte[]>), getBytes, false); } catch(Exception) { }
                try { result.fromBytes = (FromBytesHandler) Delegate.CreateDelegate(typeof(FromBytesHandler), fromBytes, false); } catch(Exception) { }

                if(result.getBytes == null) {
                    result.getBytes = (value, lowPrecision) => (byte[]) getBytes.Invoke(null, new object[] { value, lowPrecision });
                }
                if(result.fromBytes == null) {
                    result.fromBytes = (byte[] value, out int bytesRead, int startIndex, bool lowPrecision) => {
                        object[] parameters = new object[] { value, 0, startIndex, lowPrecision };
                        object read = fromBytes.Invoke(null, parameters);
                        bytesRead = (int) parameters[1];
                        return (T) read;
                    };
                }

                legacy = result;
                return result;
            }
        }
        #endregion

        #region Used by the serializer
        internal sealed override Type ValueType { get { return typeof(T); } }

        internal sealed override void WriteBoxed(BinaryNetStream stream, object value, bool lowPrecision) {
            Write(stream, (T) value, lowPrecision);
        }

        internal sealed override object ReadBoxed(BinaryNetStream stream, bool lowPrecision) {
            return Read(stream, lowPrecision);
        }

        internal sealed override MemberCodec CreateMember(FieldInfo field, bool lowPrecision) {
            return new ValueMember<T>(field, this, lowPrecision);
        }

        internal sealed override MemberCodec CreateArrayMember(FieldInfo field, bool lowPrecision) {
            return new ArrayMember<T>(field, this, lowPrecision);
        }
        #endregion
    }

    /// <summary>
    /// Wraps a converter class that can't be instantiated (abstract, no default constructor), only its static methods get used
    /// </summary>
    internal sealed class StaticDataConverter<T> : DataConverterInterface<T> {
        public StaticDataConverter(Type source) {
            legacySource = source;
        }
    }
}
