using Netamite.Client.Definition;
using Netamite.Server.Data;
using Netamite.Server.Definition;
using System;
using System.IO;
using System.Reflection;
using Netamite.Logging;

namespace Netamite {
    public class Netamite {
        
        public static NetamiteClient MasterClient;
        public static NetamiteServer MasterServer;

        internal static Type clientinformationType = typeof(ClientInformation);

        private static bool initialized = false;
        public static void Initialize() {
            if(initialized) return;
            initialized = true;

            LoadAllAssembliesInBaseDirectory();
        }

        // Converters and packet types are discovered by scanning AppDomain.CurrentDomain.GetAssemblies(),
        // which only contains assemblies the CLR has already JIT-touched. Assemblies like Netamite.Unity
        // that are referenced but never directly used before the first (de)serialization can otherwise be
        // missing from that scan, silently breaking their converters. Force them all to load up front.
        private static void LoadAllAssembliesInBaseDirectory() {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if(string.IsNullOrEmpty(baseDir) || !Directory.Exists(baseDir)) return;

            foreach(string dllPath in Directory.GetFiles(baseDir, "Netamite*.dll")) {
                try {
                    AssemblyName name = AssemblyName.GetAssemblyName(dllPath);
                    Assembly.Load(name);
                }catch(Exception ex) {
                    Log.Err(ex);
                }
            }
        }

        public static void SetClientinfoType(Type type) {
            if(initialized) {
                throw new Exception("Netamite already initialized! Can't change Clientinfo after initialization.");
            }

            if(type.BaseType != typeof(ClientInformation)) {
                throw new Exception("Clientinfo needs to have ClientInformation as a base type.");
            }

            clientinformationType = type;
        }

        public static Type GetClientinfoType() {
            if(clientinformationType == null) {
                clientinformationType= typeof(ClientInformation);
            }
            return clientinformationType;
        }
    }
}
