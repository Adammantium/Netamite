using Netamite.Client.Definition;
using Netamite.Extension;
using Netamite.Network.Packet.Attributes;
using Netamite.Network.Packet.Exceptions;
using Netamite.Server.Data;
using Netamite.Server.Definition;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace Netamite.Network.Packet {
    [PacketDefinition((byte) PacketType.UNKNOWN)]
    public class NetPacket : IDisposable {
        private static ConcurrentDictionary<byte, Type> packetTypes = new ConcurrentDictionary<byte, Type>();

        internal static void BuildPacketImplementationCache() {
            foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                Type[] typelist = null;
                
                try {
                    typelist = assembly.GetTypes().ToArray();
                }catch(Exception) {
                    continue;
                }

                for(int i = 0; i < typelist.Length; i++) {
                    byte thisPacketType = getPacketType(typelist[i]);

                    if(thisPacketType <= 0) continue;

                    if(packetTypes.ContainsKey(thisPacketType)) continue;
                    packetTypes.TryAdd(thisPacketType, typelist[i]);
                }
            }
        }

        private static Type GetPacketImplementation(byte packetType) {
            if(packetType == (byte) PacketType.UNKNOWN) return null;

            if(packetTypes.ContainsKey((byte) packetType)) {
                return packetTypes[(byte) packetType];
            }

            Type[] typelist = Assembly.GetAssembly(typeof(NetPacket)).GetTypes().ToArray();

            for(int i = 0; i < typelist.Length; i++) {
                byte thisPacketType = getPacketType(typelist[i]);
                if(thisPacketType == packetType) {
                    if(packetTypes.ContainsKey(thisPacketType)) continue;
                    packetTypes.TryAdd(thisPacketType, typelist[i]);
                    return typelist[i];
                }
            }
            return null;
        }

        public static NetPacket ReadPacket(byte[] data, bool hasLength = false) {
            BinaryNetStream stream = new BinaryNetStream(data);

            if(hasLength) stream.ReadShort(); // Flush away the length
            byte type = stream.ReadByte(false);
            
            Type implementationType = GetPacketImplementation(type);
            if(implementationType == null) return null;

            object packetInstance = Activator.CreateInstance(implementationType);
            if(packetInstance is NetPacket) {
                NetPacket np = (NetPacket) packetInstance;
                np.SetData(data, hasLength);
                return np;
            }

            return null;
        }

        public byte[] GetData(bool writeLength = false) {
            BinaryNetStream stream = new BinaryNetStream();

            stream.Write(getPacketType());

            Type myType = GetType();
            MemberInfo[] myMembers = myType.GetMembers();

            //Log.Debug("DEBUG", myType);

            BeforeProcessing();

            for(int i = 0; i < myMembers.Length; i++) {
                MemberInfo memberInfo = myMembers[i];

                object[] atts = memberInfo.GetCustomAttributes(typeof(SyncedVar), true);

                if(atts.Length > 0) {
                    Type t = memberInfo.GetUnderlyingType();

                    if(t.IsArray) {
                        System.Collections.IList val = (System.Collections.IList) memberInfo.GetValue(this);
                        
                        if(val == null) {
                            stream.WriteArray(null);
                        } else {
                            object[] array = new object[val.Count];
                            val.CopyTo(array, 0);

                            #if DEBUG_MESSAGES
                            //Log.Debug("DEBUG", memberInfo.Name + "\t" + t + "\t" + val);
                            #endif

                            stream.WriteArray(array, (atts[0] as SyncedVar).LowPrecision);
                        }
                    } else {
                        object val = memberInfo.GetValue(this);

                        #if DEBUG_MESSAGES
                        //Log.Debug("DEBUG", memberInfo.Name + "\t" + t + "\t" + val);
                        #endif

                        stream.Write(val, (atts[0] as SyncedVar).LowPrecision);
                    }
                }
            }

            if(writeLength) {
                stream.WriteLength();
            }

            return stream.ToArray();
        }

        public void SetData(byte[] data, bool hasLength = false) {
            BinaryNetStream stream = new BinaryNetStream(data);

            if(hasLength) stream.ReadShort(); // Flush away the length

            byte dataPacketType = stream.ReadByte();
            byte myPacketType   = getPacketType();

            if(dataPacketType != myPacketType) {
                throw new PacketMismatch(myPacketType, dataPacketType);
            }

            Type myType = GetType();
            MemberInfo[] myMembers = myType.GetMembers();

            //Log.Debug("DEBUG", myType);

            for(int i = 0; i < myMembers.Length; i++) {
                MemberInfo memberInfo = myMembers[i];

                object[] atts = memberInfo.GetCustomAttributes(typeof(SyncedVar), true);

                if(atts.Length > 0) {
                    Type t = memberInfo.GetUnderlyingType();

                    try {
                        if(t.IsArray) {
                            object[] val = stream.ReadArray(t.GetElementType(), (atts[0] as SyncedVar).LowPrecision);
                        
                            if(val.Length > 0) {
                                Array filledArray = Array.CreateInstance(val[0].GetType(), val.Length);
                                Array.Copy(val, filledArray, val.Length);

                                #if DEBUG_MESSAGES
                                //Log.Debug("DEBUG", memberInfo.Name + "\t" + t + "\t" + filledArray);
                                #endif

                                memberInfo.SetValue(this, filledArray);
                            }
                        } else {
                            object val = stream.Read(t, (atts[0] as SyncedVar).LowPrecision);

                            #if DEBUG_MESSAGES
                            //Log.Debug("DEBUG", memberInfo.Name + "\t" + t + "\t" + val);
                            #endif

                            memberInfo.SetValue(this, val);
                        }
                    }catch(Exception ex) {
                        throw new Exception("Error on " + myType + " while reading " + memberInfo.Name + "\n" + ex.Message);
                    }
                }
            }
        }

        public byte getPacketType() {
            return getPacketType(GetType());
        }

        private static byte getPacketType(Type type) {
            try {
                Attribute attribute = type.GetCustomAttribute(typeof(PacketDefinition), true);
                if(attribute != null) { 
                    return ((PacketDefinition) attribute).packetType;
                }
            }catch(Exception) { }
            return (byte) PacketType.UNKNOWN;
        }

        public bool IsReliable() {
            Attribute attribute = GetType().GetCustomAttribute(typeof(PacketDefinition), true);
            if(attribute != null) {
                return ((PacketDefinition) attribute).reliable;
            }
            return true;
        }

        public void Dispose() { }
        
        /// <summary>
        /// This function will be called once the packet is ready for sending and will be converted to a binary stream.
        /// You can use this function to add timestamps or do some last calculations
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
