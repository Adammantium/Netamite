using NAudio.Wave;
using Netamite.Logging;
using Netamite.Voice.Packet;
using System;

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

        public VoiceReader(VoiceClient voiceClient) {
            this.voiceClient = voiceClient;

            Input = new WaveInEvent();
            Input.WaveFormat = voiceClient.Format;
            Input.BufferMilliseconds = voiceClient.Latency;
            Input.DataAvailable += NewInputData;

            Input.RecordingStopped += Input_RecordingStopped;
        }

        public void Start() {
            Input.StartRecording();
            recording = true;
        }

        public void Stop() {
            Input.StopRecording();
            recording = false;
        }

        private int additional_samples = 0;
        private void NewInputData(object sender, WaveInEventArgs args) {
            float max = GetAudioPeak(args, MinVolumePeak);

            if(max > MinVolumePeak || additional_samples > 0) {
                if(max > MinVolumePeak) additional_samples = SamplePrefixCount;
                else additional_samples--;

                OnAudioPacketReady.Invoke(new AudioPacket(voiceClient.client.ClientId, voiceClient.Codec.Encode(args.Buffer)));
            }
        }

        private float GetAudioPeak(WaveInEventArgs args, float threshold = 1f) {
            switch(voiceClient.Format.BitsPerSample) {
                case 16: return GetAudioPeak16bit(args, threshold);
                case 32: return GetAudioPeak32bit(args, threshold);
                default: break;
            }

            return 0;
        }

        private float GetAudioPeak16bit(WaveInEventArgs args, float threshold = 1f) {
            float max = 0;
            // interpret as 16 bit audio
            for(int index = 0; index < args.BytesRecorded; index += 2) {
                short sample = (short)((args.Buffer[index + 1] << 8) |
                                        args.Buffer[index + 0]);
                // to floating point
                var sample32 = sample / 32768f;
                // absolute value 
                if(sample32 < 0) sample32 = -sample32;
                // is this the max value?
                if(sample32 > max) max = sample32;

                if(max > threshold) break;
            }
            return max;
        }
        private float GetAudioPeak32bit(WaveInEventArgs args, float threshold = 1f) {
            float max = 0;
            var buffer = new WaveBuffer(args.Buffer);
            // interpret as 32 bit floating point audio
            for(int index = 0; index < args.BytesRecorded / 4; index++) {
                var sample = buffer.FloatBuffer[index];

                // absolute value 
                if(sample < 0) sample = -sample;
                // is this the max value?
                if(sample > max) max = sample;

                if(max > threshold) break;
            }
            return max;
        }

        internal void SetDevice(int deviceId) {
            if(WaveInEvent.DeviceCount > deviceId) {
                Input.DeviceNumber = deviceId;
                if(recording) {
                    Input.StopRecording();
                }
            }
        }

        private void Input_RecordingStopped(object sender, StoppedEventArgs e) {
            if(recording)
                Input.StartRecording();
        }
    }
}
