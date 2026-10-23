using Netamite.Extension;
using Netamite.Network.Packet.Attributes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Netamite.Network.Packet {
    internal class SyncedMember {
        public readonly MemberInfo Member;
        public readonly Type Type;
        public readonly SyncedVar Attribute;

        public SyncedMember(MemberInfo member, SyncedVar attribute) {
            Member = member;
            Type = member.GetUnderlyingType();
            Attribute = attribute;
        }
    }

    /// <summary>
    /// GetMembers() and GetCustomAttributes() are slow and allocate on every call, especially on IL2CPP.
    /// The [SyncedVar] members of a type never change, so they only get looked up once per type.
    /// </summary>
    internal static class SyncedMemberCache {
        private static readonly ConcurrentDictionary<Type, SyncedMember[]> members = new ConcurrentDictionary<Type, SyncedMember[]>();

        /// <summary>
        /// All [SyncedVar] members of the type, in GetMembers() order, which is the order they are (de)serialized in
        /// </summary>
        public static SyncedMember[] Get(Type type) {
            SyncedMember[] result;
            if(members.TryGetValue(type, out result)) return result;

            return members.GetOrAdd(type, Build);
        }

        private static SyncedMember[] Build(Type type) {
            List<SyncedMember> result = new List<SyncedMember>();

            foreach(MemberInfo member in type.GetMembers()) {
                object[] atts = member.GetCustomAttributes(typeof(SyncedVar), true);
                if(atts.Length > 0) result.Add(new SyncedMember(member, (SyncedVar) atts[0]));
            }

            return result.ToArray();
        }

        private static readonly ConcurrentDictionary<Type, PacketDefinition> packetDefinitions = new ConcurrentDictionary<Type, PacketDefinition>();

        /// <summary>
        /// The PacketDefinition of a packet type, null if it has none
        /// </summary>
        public static PacketDefinition GetPacketDefinition(Type type) {
            PacketDefinition result;
            if(packetDefinitions.TryGetValue(type, out result)) return result;

            return packetDefinitions.GetOrAdd(type, t => (PacketDefinition) t.GetCustomAttribute(typeof(PacketDefinition), true));
        }
    }
}
