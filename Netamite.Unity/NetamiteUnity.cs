using Netamite.Logging;
using UnityEngine;

namespace Netamite.Unity {
    internal class NetamiteUnity {

        public static void Initialize() {
            Log.onLogMessage += OnLogMessage;
            Log.loggerType = Log.LoggerType.EVENT_ONLY;
        }

        private static void OnLogMessage(Log.Type logType, string message) {
            switch(logType) {
                case Log.Type.DEBUG:
                    Debug.Log(message);
                    break;
                
                case Log.Type.INFO:
                    Debug.Log(message);
                    break;

                case Log.Type.WARNING:
                    Debug.LogWarning(message);
                    break;

                case Log.Type.ERROR:
                    Debug.LogError(message);
                    break;

            }
        }
    }
}
