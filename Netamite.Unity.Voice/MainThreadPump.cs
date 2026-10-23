using UnityEngine;

namespace Netamite.Unity.Voice {
    /// <summary>
    /// Plays the received voice data on the main thread, see UnityVoiceClient.PlayReceivedAudio
    /// </summary>
    internal class MainThreadPump : MonoBehaviour {
        internal UnityVoiceClient voiceClient;

        private void Update() {
            voiceClient?.PlayReceivedAudio();
        }
    }
}
