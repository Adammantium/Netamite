using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Netamite.Voice.Audio {
    internal class AudioSource {

        private WaveOutEvent Output;
        private MixingSampleProvider Mixer;
        private BufferedWaveProvider waveProvider;
        private VolumeSampleProvider volumeSampleProvider;

        private VoiceClient voiceClient;

        private bool playing = false;

        public AudioSource(VoiceClient voiceClient) {
            this.voiceClient = voiceClient;

            Mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(voiceClient.Format.SampleRate, voiceClient.Format.Channels));
            Mixer.ReadFully = true;

            Output = new WaveOutEvent();
            Output.DesiredLatency = voiceClient.Latency + 50;
            Output.DeviceNumber = voiceClient._OutputDeviceId;
            Output.Init(Mixer);

            waveProvider = new BufferedWaveProvider(voiceClient.Format);
            waveProvider.BufferLength = (32 / 8) * voiceClient.Format.SampleRate * voiceClient.Format.Channels * 5; // 5 seconds of buffer.
            volumeSampleProvider = new VolumeSampleProvider(waveProvider.ToSampleProvider());

            Mixer.AddMixerInput(volumeSampleProvider);
        }

        public void Start() {
            Output.Play();
            playing = true;
        }

        public void Stop() {
            Output.Stop();

            Mixer.RemoveAllMixerInputs();

            playing = false;
        }


        public void QueuePlay(byte[] bytes) {
            if(!playing) return; // Dont process data if audio source is stopped

            byte[] data = voiceClient.Codec.Decode(bytes);

            waveProvider.AddSamples(data, 0, data.Length);
        }

        internal void SetVolume(float volume) {
            volumeSampleProvider.Volume = volume;
        }

        internal void SetDevice(int deviceId) {
            Output.DeviceNumber = deviceId;
        }
    }
}
