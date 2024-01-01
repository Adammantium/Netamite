using NAudio.Wave;
using Netamite.Voice.Packet;

namespace Netamite.Voice {
    public class VoiceReader {
        public WaveInEvent Input;

        private VoiceClient voiceClient;

        public delegate void onAudioPacketReady(AudioPacket packet);
        public onAudioPacketReady OnAudioPacketReady;


        public VoiceReader(VoiceClient voiceClient) {
            this.voiceClient = voiceClient;

            Input = new WaveInEvent();
            Input.WaveFormat = voiceClient.Format;
            Input.BufferMilliseconds = voiceClient.Latency;
            Input.DataAvailable += NewInputData;
        }

        public void Start() {
            Input.StartRecording();
        }

        public void Stop() {
            Input.StopRecording();
        }

        private void NewInputData(object sender, WaveInEventArgs e) {
            OnAudioPacketReady.Invoke(new AudioPacket(voiceClient.client.ClientId, voiceClient.Codec.Encode(e.Buffer)));
        }

    }
}
