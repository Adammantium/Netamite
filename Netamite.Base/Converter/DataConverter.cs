using System;
using System.Reflection;

namespace Netamite.Converter {
    public class DataConverter {

        public static byte[] GetData(object value, bool lowPrecision = false) {
            Type converter = GetConverterForType(value.GetType());

            if(converter != null)
                return (byte[]) converter.GetMethod("GetData").Invoke(null, new object[] { value, lowPrecision });

            throw new MissingDataConverterException(value.GetType());
        }

        // This function doesn't need to be replaced
        public static object FromData(Type type, byte[] value, int startIndex = 0, bool lowPrecision = false) {
            return FromData(type, value, out _, startIndex, lowPrecision);
        }

        public static object FromData(Type type, byte[] value, out int bytesRead, int startIndex = 0, bool lowPrecision = false) {
            bytesRead = 0;
            Type converter = GetConverterForType(type);
            
            if(converter != null)
                return converter.GetMethod("FromData").Invoke(null, new object[] { value, bytesRead, startIndex, lowPrecision });

            throw new MissingDataConverterException(type);
        }
        
        private static Type GetConverterForType(Type to_find) {
            foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                foreach(Type t in assembly.GetTypes()) {
                    if(t.BaseType == null) continue;
                    if(!t.BaseType.IsGenericType) continue;
                    if(t.BaseType.GetGenericTypeDefinition() == typeof(DataConverterInterface<>)) {
                        foreach(Type arg in t.BaseType.GetGenericArguments()) {
                            if(arg == to_find) {
                                return t;
                            }
                        }
                    }
                }
            }
            return null;
        }
    }
}
