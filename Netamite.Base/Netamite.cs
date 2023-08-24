using Netamite.Client.Definition;
using Netamite.Logging;
using Netamite.Server.Data;
using Netamite.Server.Definition;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Netamite {
    public class Netamite {
        
        public static NetamiteClient MasterClient;
        public static NetamiteServer MasterServer;

        internal static Type clientinformationType = typeof(ClientInformation);

        private static bool initialized = false;
        public static void Initialize() {
            if(initialized) return;
            
            // Brute force other netamite dlls so we can make sure all DataConverters are loaded into RAM
            // TODO: Make nicer in the future
            string[] files = Directory.GetFiles(".", "*.dll");
            foreach( string file in files ) {
                if(file.ToLower().Contains("netamite.")) {
                    Assembly assembly = Assembly.LoadFrom(file);
                }
            }

            initialized = true;
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
