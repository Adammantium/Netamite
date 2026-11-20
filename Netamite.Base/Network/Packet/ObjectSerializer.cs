using Netamite.Converter;
using System;
using System.Collections.Concurrent;

namespace Netamite.Network.Packet {
    /// <summary>
    /// Reads and writes all [SyncedVar] members of one type. Built once per type and reused for every packet.
    /// The members are written in the order SyncedMemberCache returns them, which is the wire format.
    /// </summary>
    internal sealed class ObjectSerializer {
        private static volatile ConcurrentDictionary<Type, ObjectSerializer> serializers = new ConcurrentDictionary<Type, ObjectSerializer>();

        public static ObjectSerializer Get(Type type) {
            DataConverter.RefreshIfNeeded(); // Assemblies that got loaded in the meantime can bring new converters

            ObjectSerializer result;
            if(serializers.TryGetValue(type, out result)) return result;

            return serializers.GetOrAdd(type, t => new ObjectSerializer(t));
        }

        /// <summary>
        /// Forget everything, the serializers get rebuilt on their next use.
        /// Needed when new converters show up, as the old serializers have been built without knowing them.
        /// </summary>
        internal static void Invalidate() {
            serializers = new ConcurrentDictionary<Type, ObjectSerializer>();
        }

        public readonly Type Type;
        private readonly MemberCodec[] members;
        private readonly Func<object> constructor;

        /// <summary>
        /// Packet id of the type, 0 (PacketType.UNKNOWN) if it is no packet
        /// </summary>
        public readonly byte PacketType;

        private ObjectSerializer(Type type) {
            Type = type;

            SyncedMember[] synced = SyncedMemberCache.Get(type);
            members = new MemberCodec[synced.Length];
            for(int i = 0; i < synced.Length; i++) {
                members[i] = MemberCodec.Create(synced[i]);
            }

            constructor = FieldAccess.Constructor(type);

            PacketDefinition definition = SyncedMemberCache.GetPacketDefinition(type);
            PacketType = definition != null ? definition.packetType : (byte) Packet.PacketType.UNKNOWN;
        }

        public object Create() {
            return constructor();
        }

        public void Write(BinaryNetStream stream, object target) {
            MemberCodec[] members = this.members;
            for(int i = 0; i < members.Length; i++) {
                members[i].Write(stream, target);
            }
        }

        public void Read(BinaryNetStream stream, object target) {
            MemberCodec[] members = this.members;
            int i = 0;
            try {
                for(; i < members.Length; i++) {
                    members[i].Read(stream, target);
                }
            } catch(Exception ex) {
                throw new Exception("Error on " + Type + " while reading " + members[i].Name + ": " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Creates a new object and fills it from the stream
        /// </summary>
        public object ReadNew(BinaryNetStream stream) {
            if(members.Length == 0) {
                // No [SyncedVar] members and no DataConverter for this type: reading it would silently
                // consume zero bytes and desync every field that follows. Fail loudly instead.
                throw new MissingDataConverterException(Type);
            }

            object result = constructor();
            Read(stream, result);
            return result;
        }
    }
}
