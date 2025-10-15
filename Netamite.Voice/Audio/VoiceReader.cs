using NAudio.Wave;
using Netamite.Voice.Packet;

namespace Netamite.Voice {
    public class VoiceReader {
        public WaveInEvent Input;

        private VoiceClient voiceClient;

        public delegate void onAudioPacketReady(AudioPacket packet);
        public onAudioPacketReady OnAudioPacketReady;

        /// <summary>
        /// Min Volume before sending data at all
        /// Range 0.0 - 1.0 (inclusive)
        /// </summary>
        public float MinVolumePeak = 0.04f;

        /// <summary>
        /// How many samples should be processed after the microphone is not reaching its peak anymore, so it wont suddenly cut off
        /// </summary>
        public int SamplePrefixCount = 5;

        private bool recording = false;
        private bool usingExternalAudio = false;
        private int additional_samples = 0;

        public VoiceReader(VoiceClient voiceClient, bool useExternalAudio = false) {
            this.voiceClient = voiceClient;
            this.usingExternalAudio = useExternalAudio;

            if (!useExternalAudio) {
                Input = new WaveInEvent();
                Input.WaveFormat = voiceClient.Format;
                Input.BufferMilliseconds = voiceClient.Latency;
                Input.DataAvailable += NewInputData;
                Input.RecordingStopped += Input_RecordingStopped;
            }
        }

        public void Start() {
            if (!usingExternalAudio && Input != null) {
                Input.StartRecording();
            }
            recording = true;
        }

        public void Stop() {
            if (!usingExternalAudio && Input != null) {
                Input.StopRecording();
            }
            recording = false;
        }

        /// <summary>
        /// Process external audio data from any source (e.g., Unity microphone)
        /// Assumes 16-bit PCM audio in the format expected by the codec
        /// </summary>
        /// <param name="audioData">Raw 16-bit PCM audio buffer</param>
        public void ProcessExternalAudio(byte[] audioData)
        {
            // Only process if recording is active
            if (recording == false) return;
            
            float max = GetAudioPeak(audioData, audioData.Length, MinVolumePeak);

            if (max > MinVolumePeak || additional_samples > 0) {
                if (max > MinVolumePeak) additional_samples = SamplePrefixCount;
                else additional_samples--;

                OnAudioPacketReady?.Invoke(new AudioPacket(voiceClient.client.ClientId, voiceClient.Codec.Encode(audioData)));
            }
        }

        private void NewInputData(object sender, WaveInEventArgs args) {
            float max = GetAudioPeak(args, MinVolumePeak);

            if (max > MinVolumePeak || additional_samples > 0) {
                if (max > MinVolumePeak) additional_samples = SamplePrefixCount;
                else additional_samples--;

                OnAudioPacketReady?.Invoke(new AudioPacket(voiceClient.client.ClientId, voiceClient.Codec.Encode(args.Buffer)));
            }
        }

        private float GetAudioPeak(WaveInEventArgs args, float threshold = 1f) {
            return GetAudioPeak(args.Buffer, args.BytesRecorded, threshold);
        }

        private float GetAudioPeak(byte[] buffer, int bytesRecorded, float threshold = 1f) {
            switch (voiceClient.Format.BitsPerSample) {
                case 16: return GetAudioPeak16bit(buffer, bytesRecorded, threshold);
                case 32: return GetAudioPeak32bit(buffer, bytesRecorded, threshold);
                default: break;
            }

            return 0;
        }

        private float GetAudioPeak16bit(byte[] buffer, int bytesRecorded, float threshold = 1f) {
            float max = 0;
            // interpret as 16 bit audio
            for (int index = 0; index < bytesRecorded; index += 2) {
                short sample = (short)((buffer[index + 1] << 8) |
                                        buffer[index + 0]);
                // to floating point
                var sample32 = sample / 32768f;
                // absolute value
                if (sample32 < 0) sample32 = -sample32;
                // is this the max value?
                if (sample32 > max) max = sample32;

                if (max > threshold) break;
            }
            return max;
        }

        private float GetAudioPeak32bit(byte[] buffer, int bytesRecorded, float threshold = 1f) {
            float max = 0;
            var waveBuffer = new WaveBuffer(buffer);
            // interpret as 32 bit floating point audio
            for (int index = 0; index < bytesRecorded / 4; index++) {
                var sample = waveBuffer.FloatBuffer[index];

                // absolute value
                if (sample < 0) sample = -sample;
                // is this the max value?
                if (sample > max) max = sample;

                if (max > threshold) break;
            }
            return max;
        }

        internal void SetDevice(int deviceId) {
            if (!usingExternalAudio && Input != null && WaveInEvent.DeviceCount > deviceId) {
                Input.DeviceNumber = deviceId;
                if (recording) {
                    Input.StopRecording();
                }
            }
        }

        private void Input_RecordingStopped(object sender, StoppedEventArgs e) {
            if (recording && !usingExternalAudio && Input != null)
                Input.StartRecording();
        }
    }
}
