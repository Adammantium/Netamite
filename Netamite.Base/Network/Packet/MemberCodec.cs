using Netamite.Converter;
using Netamite.Network.Packet.Attributes;
using System;
using System.Reflection;

namespace Netamite.Network.Packet {
    /// <summary>
    /// Reads and writes one [SyncedVar] member of an object. Everything that needs reflection (which converter,
    /// array or not, how to get to the field) is figured out once per member instead of once per packet.
    /// </summary>
    internal abstract class MemberCodec {
        public readonly string Name;

        protected MemberCodec(MemberInfo member) {
            Name = member.Name;
        }

        public abstract void Write(BinaryNetStream stream, object target);
        public abstract void Read(BinaryNetStream stream, object target);

        public static MemberCodec Create(SyncedMember member) {
            FieldInfo field = member.Member as FieldInfo;
            Type type = member.Type;
            bool lowPrecision = member.Attribute != null && member.Attribute.LowPrecision;

            // [SyncedVar] is only allowed on fields, anything else is handled the slow but generic way
            if(field == null) return new DynamicMember(member.Member, type, lowPrecision);

            if(type == typeof(byte[])) return new BytesMember(field);

            if(type.IsArray) {
                Type element = type.GetElementType();
                if(type.GetArrayRank() == 1 && !element.IsEnum) {
                    DataConverterBase elementConverter = DataConverter.Find(element);
                    if(elementConverter != null && elementConverter.ValueType == element) {
                        return elementConverter.CreateArrayMember(field, lowPrecision);
                    }
                }
                return new DynamicArrayMember(field, element, lowPrecision);
            }

            DataConverterBase converter = DataConverter.Find(type);
            if(converter != null) return converter.CreateMember(field, lowPrecision);

            return new ObjectMember(field, type, lowPrecision);
        }
    }

    /// <summary>
    /// A single value with a converter (int, float, string, Vector3, enums, ...)
    /// </summary>
    internal sealed class ValueMember<T> : MemberCodec {
        private readonly DataConverterInterface<T> converter;
        private readonly Func<object, T> getter;
        private readonly Action<object, T> setter;
        private readonly bool lowPrecision;

        public ValueMember(FieldInfo field, DataConverterInterface<T> converter, bool lowPrecision) : base(field) {
            this.converter = converter;
            this.lowPrecision = lowPrecision;
            getter = FieldAccess.Getter<T>(field);
            setter = FieldAccess.Setter<T>(field);
        }

        public override void Write(BinaryNetStream stream, object target) {
            converter.Write(stream, getter(target), lowPrecision);
        }

        public override void Read(BinaryNetStream stream, object target) {
            setter(target, converter.Read(stream, lowPrecision));
        }
    }

    /// <summary>
    /// An array of values with a converter. Wire format: ushort count, then the elements.
    /// </summary>
    internal sealed class ArrayMember<T> : MemberCodec {
        private readonly DataConverterInterface<T> converter;
        private readonly Func<object, T[]> getter;
        private readonly Action<object, T[]> setter;
        private readonly bool lowPrecision;

        public ArrayMember(FieldInfo field, DataConverterInterface<T> converter, bool lowPrecision) : base(field) {
            this.converter = converter;
            this.lowPrecision = lowPrecision;
            getter = FieldAccess.Getter<T[]>(field);
            setter = FieldAccess.Setter<T[]>(field);
        }

        public override void Write(BinaryNetStream stream, object target) {
            T[] values = getter(target);
            if(values == null) {
                stream.WriteUShort(0);
                return;
            }

            stream.WriteUShort((ushort) values.Length);
            for(int i = 0; i < values.Length; i++) {
                converter.Write(stream, values[i], lowPrecision);
            }
        }

        public override void Read(BinaryNetStream stream, object target) {
            int count = stream.ReadUShort();
            if(count == 0) return; // Empty arrays stay unset

            // The count comes from the network, don't allocate more than the packet could possibly contain
            T[] values = new T[count <= stream.UnreadLength() ? count : Math.Min(count, 16)];
            for(int i = 0; i < count; i++) {
                if(i == values.Length) Array.Resize(ref values, Math.Min(count, values.Length * 2));
                values[i] = converter.Read(stream, lowPrecision);
            }
            setter(target, values);
        }
    }

    /// <summary>
    /// byte[] gets copied in one go. Wire format is the same as any other array (ushort count, then the bytes).
    /// </summary>
    internal sealed class BytesMember : MemberCodec {
        private readonly Func<object, byte[]> getter;
        private readonly Action<object, byte[]> setter;

        public BytesMember(FieldInfo field) : base(field) {
            getter = FieldAccess.Getter<byte[]>(field);
            setter = FieldAccess.Setter<byte[]>(field);
        }

        public override void Write(BinaryNetStream stream, object target) {
            stream.WriteBytes(getter(target));
        }

        public override void Read(BinaryNetStream stream, object target) {
            byte[] value = stream.ReadBytes();
            if(value.Length > 0) setter(target, value); // Empty arrays stay unset
        }
    }

    /// <summary>
    /// A nested object without a converter, it gets written member by member
    /// </summary>
    internal sealed class ObjectMember : MemberCodec {
        private readonly Type type;
        private readonly bool lowPrecision;
        private readonly Func<object, object> getter;
        private readonly Action<object, object> setter;
        private ObjectSerializer serializer; // Looked up on first use, the type could contain itself

        public ObjectMember(FieldInfo field, Type type, bool lowPrecision) : base(field) {
            this.type = type;
            this.lowPrecision = lowPrecision;
            getter = FieldAccess.Getter<object>(field);
            setter = FieldAccess.Setter<object>(field);
        }

        public override void Write(BinaryNetStream stream, object target) {
            object value = getter(target);
            if(value == null) throw new ArgumentNullException(Name, "Can't send null, the receiver would not know what to read.");

            if(value.GetType() == type) {
                if(serializer == null) serializer = ObjectSerializer.Get(type);
                serializer.Write(stream, value);
            } else {
                stream.Write(value, lowPrecision); // Something derived is stored in the field
            }
        }

        public override void Read(BinaryNetStream stream, object target) {
            if(serializer == null) serializer = ObjectSerializer.Get(type);
            setter(target, serializer.ReadNew(stream));
        }
    }

    /// <summary>
    /// Arrays that have no converter for their elements: nested objects and enums.
    /// Wire format: ushort count, then the elements.
    /// </summary>
    internal sealed class DynamicArrayMember : MemberCodec {
        private readonly Type element;
        private readonly Type elementWireType;
        private readonly bool lowPrecision;
        private readonly Func<object, object> getter;
        private readonly Action<object, object> setter;
        private ObjectSerializer serializer; // Only for elements that get written member by member, looked up on first use
        private bool serializerResolved;

        public DynamicArrayMember(FieldInfo field, Type element, bool lowPrecision) : base(field) {
            this.element = element;
            this.elementWireType = element.IsEnum ? Enum.GetUnderlyingType(element) : element;
            this.lowPrecision = lowPrecision;
            getter = FieldAccess.Getter<object>(field);
            setter = FieldAccess.Setter<object>(field);
        }

        private ObjectSerializer ElementSerializer {
            get {
                if(!serializerResolved) {
                    serializer = (!element.IsEnum && DataConverter.Find(element) == null) ? ObjectSerializer.Get(element) : null;
                    serializerResolved = true;
                }
                return serializer;
            }
        }

        public override void Write(BinaryNetStream stream, object target) {
            Array values = (Array) getter(target);
            if(values == null) {
                stream.WriteUShort(0);
                return;
            }

            // Arrays of classes don't need to box anything
            object[] objects = values as object[];
            ObjectSerializer serializer = ElementSerializer;

            stream.WriteUShort((ushort) values.Length);
            for(int i = 0; i < values.Length; i++) {
                object value = objects != null ? objects[i] : values.GetValue(i);
                if(value == null) throw new ArgumentNullException(Name, "Can't send null inside of an array, the receiver would not know what to read.");

                if(serializer != null && value.GetType() == element) {
                    serializer.Write(stream, value);
                } else {
                    stream.Write(value, lowPrecision);
                }
            }
        }

        public override void Read(BinaryNetStream stream, object target) {
            int count = stream.ReadUShort();
            if(count == 0) return; // Empty arrays stay unset

            ObjectSerializer serializer = ElementSerializer;

            Array values = Array.CreateInstance(element, count <= stream.UnreadLength() ? count : Math.Min(count, 16));
            for(int i = 0; i < count; i++) {
                if(i == values.Length) {
                    Array grown = Array.CreateInstance(element, Math.Min(count, values.Length * 2));
                    Array.Copy(values, grown, values.Length);
                    values = grown;
                }
                object value = serializer != null ? serializer.ReadNew(stream) : stream.Read(elementWireType, lowPrecision);
                values.SetValue(element.IsEnum ? Enum.ToObject(element, value) : value, i);
            }
            setter(target, values);
        }
    }

    /// <summary>
    /// Fallback for everything that is not a plain field, goes through reflection for every access
    /// </summary>
    internal sealed class DynamicMember : MemberCodec {
        private readonly MemberInfo member;
        private readonly Type type;
        private readonly bool lowPrecision;

        public DynamicMember(MemberInfo member, Type type, bool lowPrecision) : base(member) {
            this.member = member;
            this.type = type;
            this.lowPrecision = lowPrecision;
        }

        public override void Write(BinaryNetStream stream, object target) {
            object value = Extension.MemberInfoExtension.GetValue(member, target);
            if(type.IsArray) {
                System.Collections.IList list = (System.Collections.IList) value;
                if(list == null) {
                    stream.WriteArray(null);
                } else {
                    object[] array = new object[list.Count];
                    list.CopyTo(array, 0);
                    stream.WriteArray(array, lowPrecision);
                }
            } else {
                stream.Write(value, lowPrecision);
            }
        }

        public override void Read(BinaryNetStream stream, object target) {
            if(type.IsArray) {
                object[] values = stream.ReadArray(type.GetElementType(), lowPrecision);
                if(values.Length > 0 && values[0] != null) {
                    Array filled = Array.CreateInstance(values[0].GetType(), values.Length);
                    Array.Copy(values, filled, values.Length);
                    Extension.MemberInfoExtension.SetValue(member, target, filled);
                }
            } else {
                Extension.MemberInfoExtension.SetValue(member, target, stream.Read(type, lowPrecision));
            }
        }
    }
}
