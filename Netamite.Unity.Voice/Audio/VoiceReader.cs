using UnityEngine;
using Netamite.Unity.Voice.Packet;
using System;
using System.Collections;

namespace Netamite.Unity.Voice {
    public class VoiceReader : MonoBehaviour {
        [Header("Microphone Settings")]
        [Tooltip("Device name to use for microphone input. Leave empty for default device.")]
        [SerializeField] private string microphoneDeviceName = null; // null = default microphone

        [Tooltip("Sample rate of the microphone recording. Must match codec requirements (16000 for G722).")]
        [SerializeField] private int sampleRate = 16000;

        [Tooltip("Length of audio buffer in seconds.")]
        [SerializeField] private int bufferLengthSeconds = 1;

        [Tooltip("How often to capture and process microphone data.")]
        [SerializeField] private float captureFrequency = 0.05f; // 50ms update frequency

        [Tooltip("How much the loudness is boosted for the cutoff value.")]
        [SerializeField] private float cutoffAmplifier = 10f;

        public delegate void onAudioPacketReady(UnityAudioPacket packet);
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
        
        private int additional_samples = 0;

        public static string[] AvailableMicrophoneDevices => Microphone.devices;

        // References
        private UnityVoiceClient voiceClient;
        private AudioClip microphoneClip;
        private float[] sampleBuffer;
        private byte[] pcmBuffer;
        private int previousPosition = 0;
        private Coroutine processingCoroutine;

        // State tracking
        private bool isCapturing = false;
        private bool isInitialized = false;
        private int channels = 1; // G722 is mono
        
        /// <summary>
        /// Initialize with a VoiceClient instance
        /// </summary>
        public void Initialize(UnityVoiceClient voiceClient) {
            if (isInitialized) {
                Stop();
            }

            this.voiceClient = voiceClient;
            isInitialized = true;

            Start();
        }


        /// <summary>
        /// Start microphone capture
        /// </summary>
        public void Start() {
            if (!isInitialized) {
                Debug.LogError("[MicrophoneCapture] Cannot start capture: VoiceClient not initialized. Call Initialize() first.");
                return;
            }

            if (isCapturing) {
                return;
            }

            // Log available microphone devices
            Debug.Log("[MicrophoneCapture] Available microphones:");
            foreach (string device in Microphone.devices) {
                Debug.Log($"- {device}");
            }

            // Start microphone recording
            microphoneClip = Microphone.Start(microphoneDeviceName, true, bufferLengthSeconds, sampleRate);

            // Wait for microphone to initialize
            while (Microphone.GetPosition(microphoneDeviceName) <= 0) { }

            // Prepare buffers - calculate appropriate buffer size based on capture frequency
            int samplesPerCapture = Mathf.CeilToInt(sampleRate * channels * captureFrequency);
            sampleBuffer = new float[samplesPerCapture];
            pcmBuffer = new byte[samplesPerCapture * 2]; // 16-bit = 2 bytes per sample

            // Start processing
            processingCoroutine = StartCoroutine(ProcessMicrophone());
            isCapturing = true;

            Debug.Log($"[MicrophoneCapture] Started capturing from '{(string.IsNullOrEmpty(microphoneDeviceName) ? "default device" : microphoneDeviceName)}' at {sampleRate}Hz");
        }

        /// <summary>
        /// Stop microphone capture
        /// </summary>
        public void Stop() {
            if (!isCapturing) {
                return;
            }

            if (processingCoroutine != null) {
                StopCoroutine(processingCoroutine);
                processingCoroutine = null;
            }

            Microphone.End(microphoneDeviceName);
            microphoneClip = null;
            isCapturing = false;

            Debug.Log("[MicrophoneCapture] Stopped capturing microphone");
        }
        
        internal void SetDevice(string deviceName) {
            if (deviceName == microphoneDeviceName) {
                return;
            }

            bool wasCapturing = isCapturing;

            if (isCapturing) {
                Stop();
            }

            microphoneDeviceName = deviceName;

            if (wasCapturing && isInitialized) {
                Start();
            }
        }



        private IEnumerator ProcessMicrophone() {
            while (true) {
                if (microphoneClip == null) {
                    yield return null;
                    continue;
                }

                int currentPosition = Microphone.GetPosition(microphoneDeviceName);

                if (currentPosition != previousPosition) {
                    // Get new data
                    int readPos = previousPosition;
                    int samplesAvailable;

                    if (currentPosition < previousPosition) {
                        // Wraparound case
                        samplesAvailable = (microphoneClip.samples - previousPosition) + currentPosition;
                    } else {
                        samplesAvailable = currentPosition - previousPosition;
                    }

                    // If we have enough samples to process
                    if (samplesAvailable >= sampleBuffer.Length) {
                        // Get audio samples from Unity's AudioClip
                        microphoneClip.GetData(sampleBuffer, readPos % microphoneClip.samples);

                        if (IsLoudEnough(sampleBuffer)) {
                            // Convert float samples to 16-bit PCM
                            ConvertFloatToPCM16(sampleBuffer, pcmBuffer);
                            
                            OnAudioPacketReady?.Invoke(new UnityAudioPacket(voiceClient.client.ClientId, voiceClient.Codec.Encode(pcmBuffer)));
                        }

                        // Update position
                        previousPosition = (readPos + sampleBuffer.Length) % microphoneClip.samples;
                    }
                }

                yield return new WaitForSeconds(captureFrequency);
            }
        }

        public bool IsLoudEnough(float[] sampleData) {
            float totalLoudness = 0f;

            for (int i = 0; i < sampleData.Length; ++i) {
                totalLoudness += Math.Abs(sampleData[i]);
            }

            return (totalLoudness / sampleData.Length) * cutoffAmplifier >= MinVolumePeak;
        }

        /// <summary>
        /// Converts Unity's float audio format [-1.0, 1.0] to 16-bit PCM format required by the voice codec
        /// </summary>
        private void ConvertFloatToPCM16(float[] samples, byte[] output) {
            // Ensure buffer sizes match our expectations
            int bytesToWrite = Math.Min(samples.Length * 2, output.Length);
            int samplesToConvert = bytesToWrite / 2;

            for (int i = 0; i < samplesToConvert; i++) {
                // Convert float [-1.0, 1.0] to short [-32768, 32767]
                short value = (short)(samples[i] * 32767f);

                // Write as little-endian (LSB first)
                output[i * 2] = (byte)(value & 0xff);
                output[i * 2 + 1] = (byte)((value >> 8) & 0xff);
            }
        }

        private void OnDisable() {
            if (isCapturing) {
                Stop();
            }
        }
    }
}
