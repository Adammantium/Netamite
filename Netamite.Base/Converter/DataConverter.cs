using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Netamite.Converter {
    public class DataConverter {

        public static byte[] GetBytes(object value, bool lowPrecision = false) {
            Type converter = GetConverterForType(value.GetType());

            if(converter != null)
                return (byte[]) GetConverterMethod(getBytesMethods, converter, "GetBytes").Invoke(null, new object[] { value, lowPrecision });

            throw new MissingDataConverterException(value.GetType());
        }

        // This function doesn't need to be replaced
        public static object FromBytes(Type type, byte[] value, int startIndex = 0, bool lowPrecision = false) {
            return FromBytes(type, value, out _, startIndex, lowPrecision);
        }

        public static object FromBytes(Type type, byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            bytesRead = 0;
            Type converter = GetConverterForType(type);
            
            if(converter != null) {
                object[] parameters = new object[] { value, bytesRead, startIndex, lowPrecision };
                object result = null;
                try {
                    result = GetConverterMethod(fromBytesMethods, converter, "FromBytes").Invoke(null, parameters);
                }catch(System.Reflection.TargetInvocationException ex) {
                    Logging.Log.Err(ex.InnerException);
                    throw ex;
                }
                if(result != null) {
                    bytesRead = (int) parameters[1];
                }
                return result;
            }

            throw new MissingDataConverterException(type);
        }
        
        // GetMethod is a slow, allocating lookup and runs for every single value, so cache it per converter
        private static readonly ConcurrentDictionary<Type, MethodInfo> getBytesMethods = new ConcurrentDictionary<Type, MethodInfo>();
        private static readonly ConcurrentDictionary<Type, MethodInfo> fromBytesMethods = new ConcurrentDictionary<Type, MethodInfo>();

        private static MethodInfo GetConverterMethod(ConcurrentDictionary<Type, MethodInfo> cache, Type converter, string name) {
            MethodInfo method;
            if(cache.TryGetValue(converter, out method)) return method;

            return cache.GetOrAdd(converter, c => c.GetMethod(name));
        }

        private static ConcurrentDictionary<Type, Type> converters = new ConcurrentDictionary<Type, Type>();
        private static Type GetConverterForType(Type to_find) {
            if(to_find.IsEnum) {
                to_find = to_find.GetEnumUnderlyingType();
            }

            if(converters.ContainsKey(to_find)) {
                Type converter;
                if(converters.TryGetValue(to_find, out converter) && converter != null) {
                    return converter;
                }
            }

            foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                try {
                    foreach(Type t in assembly.GetTypes()) {
                        if(t.BaseType == null) continue;
                        if(!t.BaseType.IsGenericType) continue;
                        if(t.BaseType.GetGenericTypeDefinition() == typeof(DataConverterInterface<>)) {
                            foreach(Type arg in t.BaseType.GetGenericArguments()) {
                                if(arg == to_find) {
                                    converters.TryAdd(to_find, t);
                                    return t;
                                }
                            }
                        }
                    }
                }catch(ReflectionTypeLoadException) { } // Ignore it if a assembly cant be fully checked
            }
            return null;
        }
    }
}
