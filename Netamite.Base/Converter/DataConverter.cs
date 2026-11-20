using Netamite.Logging;
using Netamite.Network;
using Netamite.Network.Packet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Netamite.Converter {
    public class DataConverter {

        public static byte[] GetBytes(object value, bool lowPrecision = false) {
            DataConverterBase converter = Find(value.GetType());
            if(converter == null) throw new MissingDataConverterException(value.GetType());

            BinaryNetStream stream = BinaryNetStream.RentWriter();
            try {
                converter.WriteBoxed(stream, value, lowPrecision);
                return stream.ToArray();
            } finally {
                BinaryNetStream.ReturnWriter(stream);
            }
        }

        // This function doesn't need to be replaced
        public static object FromBytes(Type type, byte[] value, int startIndex = 0, bool lowPrecision = false) {
            return FromBytes(type, value, out _, startIndex, lowPrecision);
        }

        public static object FromBytes(Type type, byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            bytesRead = 0;
            DataConverterBase converter = Find(type);
            if(converter == null) throw new MissingDataConverterException(type);

            BinaryNetStream stream = BinaryNetStream.RentReader();
            try {
                stream.SetBuffer(value, startIndex, value.Length - startIndex);
                object result = converter.ReadBoxed(stream, lowPrecision);
                if(result != null) {
                    bytesRead = (value.Length - startIndex) - stream.UnreadLength();
                }
                return result;
            } finally {
                BinaryNetStream.ReturnReader(stream);
            }
        }

        #region Converter lookup
        // Converter of every type that has been asked for so far, null for types without one. Enums use the converter of their underlying type
        private static volatile ConcurrentDictionary<Type, DataConverterBase> converters = new ConcurrentDictionary<Type, DataConverterBase>();

        // All converter classes that exist: converted type => converter class
        private static readonly Dictionary<Type, Type> converterTypes = new Dictionary<Type, Type>();
        private static readonly object scanLock = new object();

        /// <summary>
        /// The converter that handles the type, null if there is none
        /// </summary>
        internal static DataConverterBase Find(Type type) {
            RefreshIfNeeded();

            DataConverterBase converter;
            if(converters.TryGetValue(type, out converter)) return converter;

            Type lookup = type.IsEnum ? Enum.GetUnderlyingType(type) : type;
            Type converterType;
            lock(scanLock) {
                converterTypes.TryGetValue(lookup, out converterType);
            }
            converter = converterType != null ? CreateConverter(lookup, converterType) : null;

            return converters.GetOrAdd(type, converter);
        }

        private static DataConverterBase CreateConverter(Type valueType, Type converterType) {
            try {
                if(!converterType.IsAbstract && converterType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null) != null) {
                    return (DataConverterBase) Activator.CreateInstance(converterType, true);
                }
                // Can't be instantiated, but its static GetBytes / FromBytes methods still work
                return (DataConverterBase) Activator.CreateInstance(typeof(StaticDataConverter<>).MakeGenericType(valueType), converterType);
            } catch(Exception e) {
                Log.Err(Defines.NETAMITE, $"Converter {converterType} for {valueType} can't be used: {e}");
                return null;
            }
        }

        /// <summary>
        /// Looks through the assembly for converters, a converter is every class that directly extends DataConverterInterface.
        /// If multiple converters exist for one type the first one wins.
        /// </summary>
        /// <returns>true if the assembly contained converters that were unknown so far</returns>
        private static bool ScanForConverters(Type[] types) {
            bool found = false;
            foreach(Type t in types) {
                if(t == null) continue;
                Type baseType = t.BaseType;
                if(baseType == null) continue;
                if(!baseType.IsGenericType) continue;
                if(t.ContainsGenericParameters) continue;
                if(t.IsGenericType && t.GetGenericTypeDefinition() == typeof(StaticDataConverter<>)) continue;
                if(baseType.GetGenericTypeDefinition() != typeof(DataConverterInterface<>)) continue;

                Type valueType = baseType.GetGenericArguments()[0];
                if(converterTypes.ContainsKey(valueType)) continue;

                converterTypes.Add(valueType, t);
                found = true;
            }
            return found;
        }
        #endregion

        #region Assembly scanning
        // Converters and packets live in whatever assembly the game / mod / plugin brings along, so all of them have to be
        // looked through. That is expensive (GetTypes on everything), so every assembly gets looked at exactly once and
        // only assemblies that got loaded since the last time trigger a new scan.
        private static readonly HashSet<Assembly> scannedAssemblies = new HashSet<Assembly>();
        private static volatile bool assembliesChanged = true;
        private static bool listeningForAssemblies = false;

        internal static void RefreshIfNeeded() {
            if(assembliesChanged) ScanAssemblies();
        }

        private static void ScanAssemblies() {
            lock(scanLock) {
                if(!assembliesChanged) return;

                if(!listeningForAssemblies) {
                    listeningForAssemblies = true;
                    try {
                        AppDomain.CurrentDomain.AssemblyLoad += (sender, args) => assembliesChanged = true;
                    } catch(Exception) { } // Runtimes that can't load assemblies later on have nothing to tell us
                }

                // Reset first, an assembly that gets loaded while scanning must cause another scan
                assembliesChanged = false;

                bool newConverters = false;
                foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                    if(!scannedAssemblies.Add(assembly)) continue;
                    if(assembly.IsDynamic) continue; // Generated at runtime (Harmony patches and the like), no packets in there

                    Type[] types;
                    try {
                        types = assembly.GetTypes();
                    } catch(ReflectionTypeLoadException e) {
                        types = e.Types; // Whatever could be loaded, the rest is null
                    } catch(Exception) {
                        continue;
                    }
                    if(types == null) continue;

                    newConverters |= ScanForConverters(types);
                    NetPacket.ScanForPackets(types);
                }

                if(newConverters) {
                    // Everything built so far might have been built without knowing those converters
                    converters = new ConcurrentDictionary<Type, DataConverterBase>();
                    ObjectSerializer.Invalidate();
                    NetPacket.InvalidateSerializers();
                }
            }
        }
        #endregion
    }
}
