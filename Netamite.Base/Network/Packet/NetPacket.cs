using Netamite.Client.Definition;
using Netamite.Converter;
using Netamite.Converter.Implementation;
using Netamite.Logging;
using Netamite.Network.Packet.Exceptions;
using Netamite.Server.Data;
using Netamite.Server.Definition;
using System;
using System.Threading;

namespace Netamite.Network.Packet {
    [PacketDefinition((byte) PacketType.UNKNOWN)]
    public class NetPacket : IDisposable {
        #region Packet types
        // Packet id => class that implements it. Ids are bytes, so a plain array is all the lookup that is needed
        private static readonly Type[] packetTypes = new Type[256];
        private static volatile ObjectSerializer[] packetSerializers = new ObjectSerializer[256];

        /// <summary>
        /// Makes sure the packets of all loaded assemblies are known.
        /// Assemblies that have been looked through before are skipped, so calling this repeatedly is cheap.
        /// </summary>
        internal static void BuildPacketImplementationCache() {
            DataConverter.RefreshIfNeeded();
            Prewarm();
        }

        /// <summary>
        /// Registers all packets of an assembly, called for every assembly exactly once.
        /// A packet is a non abstract NetPacket with a PacketDefinition, if an id is used twice the first packet wins.
        /// </summary>
        internal static void ScanForPackets(Type[] types) {
            for(int i = 0; i < types.Length; i++) {
                Type type = types[i];
                if(type == null) continue;
                if(type.IsAbstract || type.ContainsGenericParameters) continue;
                if(!typeof(NetPacket).IsAssignableFrom(type)) continue; // Way cheaper than asking every type for its attributes

                byte id = getPacketType(type);
                if(id <= 0) continue;

                lock(packetTypes) {
                    if(packetTypes[id] == null) packetTypes[id] = type;
                }
            }
        }

        internal static void InvalidateSerializers() {
            packetSerializers = new ObjectSerializer[256];
        }

        private static ObjectSerializer GetPacketSerializer(byte packetType) {
            if(packetType == (byte) PacketType.UNKNOWN) return null;

            DataConverter.RefreshIfNeeded(); // Packets and converters can come from assemblies that got loaded in the meantime

            ObjectSerializer serializer = packetSerializers[packetType];
            if(serializer != null) return serializer;

            Type type = packetTypes[packetType];
            if(type == null) return null;

            ObjectSerializer[] cache = packetSerializers;
            serializer = ObjectSerializer.Get(type);
            cache[packetType] = serializer;
            return serializer;
        }

        private static int prewarming = 0;
        /// <summary>
        /// Building the serializer of a packet type takes a moment (reflection, compiling the accessors).
        /// Doing that in the background when a client / server gets created keeps it out of the first packets.
        /// </summary>
        private static void Prewarm() {
            if(Interlocked.Exchange(ref prewarming, 1) == 1) return;

            ThreadPool.QueueUserWorkItem(_ => {
                try {
                    for(int id = 1; id < 256; id++) {
                        GetPacketSerializer((byte) id);
                    }
                } catch(Exception e) {
                    Log.Debug(Defines.NETAMITE, "Preparing the packet serializers failed, they get built on first use instead: " + e.Message);
                } finally {
                    Interlocked.Exchange(ref prewarming, 0);
                }
            });
        }
        #endregion

        public static NetPacket ReadPacket(byte[] data, bool hasLength = false) {
            int offset = hasLength ? 2 : 0; // The length is of no use here, skip it
            return ReadPacket(data, offset, data.Length - offset);
        }

        /// <summary>
        /// Reads a packet (type + members, no length) from a part of the array.
        /// Nothing in the returned packet references the array, so it can be reused right away.
        /// </summary>
        /// <returns>The packet, or null if the data is no valid packet</returns>
        public static NetPacket ReadPacket(byte[] data, int offset, int count) {
            if(count < 1) return null;

            byte type = data[offset];

            BinaryNetStream stream = null;
            try {
                ObjectSerializer serializer = GetPacketSerializer(type);
                if(serializer == null) {
                    Log.Err(Defines.NETAMITE, $"No implementation for packet id {type} found.");
                    return null;
                }

                NetPacket packet = (NetPacket) serializer.Create();

                stream = BinaryNetStream.RentReader();
                stream.SetBuffer(data, offset + 1, count - 1);
                serializer.Read(stream, packet);

                return packet;
            } catch(Exception ex) {
                #if DEBUG_MESSAGES
                byte[] raw = new byte[count];
                Buffer.BlockCopy(data, offset, raw, 0, count);
                Log.Err(Defines.NETAMITE, "Failed to read packet id " + type + " (data=" + StringConverter.ToHexString(raw) + ")");
                #endif
                Log.Err(ex);
                return null;
            } finally {
                if(stream != null) BinaryNetStream.ReturnReader(stream);
            }
        }

        public byte[] GetData(bool writeLength = false) {
            BinaryNetStream stream = BinaryNetStream.RentWriter();
            try {
                WriteTo(stream, writeLength);
                return stream.ToArray();
            } finally {
                BinaryNetStream.ReturnWriter(stream);
            }
        }

        /// <summary>
        /// Appends the packet to the stream, the way it goes over the wire: [ushort length] byte type, members.
        /// The stream can already contain other packets, that is how multiple packets get sent in one go.
        /// </summary>
        internal void WriteTo(BinaryNetStream stream, bool writeLength) {
            ObjectSerializer serializer = ObjectSerializer.Get(GetType());

            int packetStart = stream.Length();
            if(writeLength) stream.WriteUShort(0); // Filled in once the length is known

            stream.WriteByte(serializer.PacketType);

            BeforeProcessing();

            serializer.Write(stream, this);

            if(writeLength) {
                int length = stream.Length() - packetStart - 2;
                if(length > ushort.MaxValue) {
                    Log.Err(Defines.NETAMITE, $"{GetType()} is {length} bytes big, packets can't be bigger than {ushort.MaxValue} bytes. The receiver won't be able to read it.");
                }
                stream.PatchUShort(packetStart, (ushort) length);
            }
        }

        public void SetData(byte[] data, bool hasLength = false) {
            int offset = hasLength ? 2 : 0; // The length is of no use here, skip it
            if(data.Length - offset < 1) throw new Exception("Could not read value of type 'byte'!");

            ObjectSerializer serializer = ObjectSerializer.Get(GetType());

            byte dataPacketType = data[offset];
            byte myPacketType   = serializer.PacketType;

            if(dataPacketType != myPacketType) {
                throw new PacketMismatch(myPacketType, dataPacketType);
            }

            BinaryNetStream stream = BinaryNetStream.RentReader();
            try {
                stream.SetBuffer(data, offset + 1, data.Length - offset - 1);
                serializer.Read(stream, this);
            } finally {
                BinaryNetStream.ReturnReader(stream);
            }
        }

        public byte getPacketType() {
            PacketDefinition definition = SyncedMemberCache.GetPacketDefinition(GetType());
            return definition != null ? definition.packetType : (byte) PacketType.UNKNOWN;
        }

        private static byte getPacketType(Type type) {
            if(type == null) return (byte)PacketType.UNKNOWN;

            try {
                PacketDefinition definition = SyncedMemberCache.GetPacketDefinition(type);
                if(definition != null) {
                    return definition.packetType;
                }
            }catch(Exception) { }
            return (byte) PacketType.UNKNOWN;
        }

        public bool IsReliable() {
            PacketDefinition definition = SyncedMemberCache.GetPacketDefinition(GetType());
            return definition != null ? definition.reliable : true;
        }

        public void Dispose() { }

        /// <summary>
        /// This function will be called once the packet is ready for sending and will be converted to a binary stream.
        /// You can use this function to add timestamps or do some last calculations.
        /// A packet that gets sent to multiple clients at once is only converted once, so this is called once for all of them.
        /// </summary>
        public virtual void BeforeProcessing() { }

        /// <summary>
        /// This will be called when the packet is received on the Client.
        /// </summary>
        /// <param name="client">Client Instance that is processing the packet</param>
        /// <returns>TRUE - Ends the processing and is not calling any ReceivedData-Events / FALSE - Will still call the ReceiveData-Event</returns>
        public virtual bool ProcessClient(NetamiteClient client) { return false; }

        /// <summary>
        /// This will be called when the packet is received on the Server.
        /// </summary>
        /// <param name="server">Server Instance that is processing the  packet</param>
        /// <param name="client">Client Information that sent the Packet</param>
        /// <returns></returns>
        public virtual bool ProcessServer(NetamiteServer server, ClientInformation client) { return false; }
    }
}
