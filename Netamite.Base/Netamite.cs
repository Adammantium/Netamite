using Netamite.Client.Definition;
using Netamite.Logging;
using Netamite.Server.Definition;
using System;
using System.IO;
using System.Reflection;

namespace Netamite {
    public class Netamite {
        
        public static NetamiteClient MasterClient;
        public static NetamiteServer MasterServer;

        private static bool initialized = false;
        internal static void Initialize() {
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
    }
}
