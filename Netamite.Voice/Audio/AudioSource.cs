using Microsoft.SqlServer.Server;
using NAudio.Mixer;
using NAudio.Wave.SampleProviders;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Netamite.Voice.Audio {
    internal class AudioSource {

        private WaveOutEvent Output;
        private MixingSampleProvider Mixer;
        private BufferedWaveProvider waveProvider;

        private VoiceClient voiceClient;

        public AudioSource(VoiceClient voiceClient) {
            this.voiceClient = voiceClient;

            Mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(voiceClient.Format.SampleRate, voiceClient.Format.Channels));
            Mixer.ReadFully = true;

            Output = new WaveOutEvent();
            Output.DesiredLatency = voiceClient.Latency + 50;
            Output.Init(Mixer);

            waveProvider = new BufferedWaveProvider(voiceClient.Format);
            waveProvider.BufferLength = (32 / 8) * voiceClient.Format.SampleRate * voiceClient.Format.Channels * 5; // 5 seconds of buffer.
            Mixer.AddMixerInput(waveProvider);
        }

        public void Start() {
            Output.Play();
        }

        public void Stop() {
            Output.Stop();

            Mixer.RemoveAllMixerInputs();
        }


        public void QueuePlay(byte[] bytes) {
            byte[] data = voiceClient.Codec.Decode(bytes);

            waveProvider.AddSamples(data, 0, data.Length);
        }

        internal void SetVolume(float volume) {
            Output.Volume = volume;
        }
    }
}
