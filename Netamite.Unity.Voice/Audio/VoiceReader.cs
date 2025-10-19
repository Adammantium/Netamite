using UnityEngine;
using Netamite.Unity.Voice.Packet;
using System;
using System.Collections;
using System.Collections.Generic;

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
        
        [Tooltip("How many packets should be held back to dont give latency for silence.")]
        [SerializeField] private int packet_holdback = 1;

        public delegate void onAudioPacketReady(UnityAudioPacket packet);
        public onAudioPacketReady OnAudioPacketReady;

        /// <summary>
        /// Min Volume before sending data at all
        /// Range 0.0 - 1.0 (inclusive)
        /// </summary>
        public float MinVolumePeak = 0.4f;

        public bool CalculateTotalVolumePeak = false;
        
        /// <summary>
        /// How many samples should be processed after the microphone is not reaching its peak anymore, so it wont suddenly cut off
        /// </summary>
        public int SampleSuffixCount = 5;
        
        private int additional_samples = 0;

        public static string[] AvailableMicrophoneDevices => Microphone.devices;

        public bool talking = false;

        /// <summary>
        /// Contains the current volume of the microphone input
        /// </summary>
        /// <value>The current volume.</value>
        public float currentVolume { get; set; }

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
        private int channels = 1; // Mono
        
        /// <summary>
        /// Initialize with a VoiceClient instance
        /// </summary>
        public void Initialize(UnityVoiceClient voiceClient) {
            if (isInitialized) {
                Stop();
            }

            this.voiceClient = voiceClient;
            isInitialized = true;

            if(voiceClient != null)
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
        
        public void SetDevice(string deviceName) {
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

        private int GetSamplesAvailable(int currentPosition) {
            int samplesAvailable;
            
            if (currentPosition < previousPosition) {
                // Wraparound case
                samplesAvailable = (microphoneClip.samples - previousPosition) + currentPosition;
            } else {
                samplesAvailable = currentPosition - previousPosition;
            }
            
            return samplesAvailable;
        }

        private Queue<UnityAudioPacket> buffer = new Queue<UnityAudioPacket>();
        
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
                    int samplesAvailable = GetSamplesAvailable(currentPosition);
                    
                    // If we have enough samples to process
                    if(samplesAvailable >= sampleBuffer.Length) {
                        // Get audio samples from Unity's AudioClip
                        microphoneClip.GetData(sampleBuffer, readPos % microphoneClip.samples);
                        
                        ConvertFloatToPCM16(sampleBuffer, pcmBuffer);
                        
                        float max = GetAudioPeak16bit(pcmBuffer, pcmBuffer.Length, MinVolumePeak);
                        currentVolume = max;

                        if (max > MinVolumePeak || additional_samples > 0) {
                            if (max > MinVolumePeak) additional_samples = SampleSuffixCount;
                            else additional_samples--;

                            if (!talking) {
                                if (buffer.Count < packet_holdback) {
                                    if(voiceClient != null) buffer.Enqueue(new UnityAudioPacket(voiceClient.client.ClientId, voiceClient.Codec.Encode(pcmBuffer)));
                                } else {
                                    while(buffer.Count > 0) {
                                        OnAudioPacketReady?.Invoke(buffer.Dequeue());
                                    }
                                    talking = true;
                                    if (voiceClient != null) voiceClient?.OnTalkingStateChanged?.Invoke(true);
                                }
                            } else {
                                if (voiceClient != null) OnAudioPacketReady?.Invoke(new UnityAudioPacket(voiceClient.client.ClientId, voiceClient.Codec.Encode(pcmBuffer)));
                            }
                        } else {
                            if (voiceClient != null) if (talking) voiceClient?.OnTalkingStateChanged?.Invoke(false);
                            talking = false;
                        }

                        // Update position
                        previousPosition = (readPos + sampleBuffer.Length) % microphoneClip.samples;
                    }
                }
                
                yield return new WaitForSeconds(captureFrequency);
            }
        }

        private float GetAudioPeak16bit(byte[] buffer, int bytesRecorded, float threshold = 1f) {
            float max = 0;
            // interpret as 16 bit audio
            for (int index = 0; index < bytesRecorded; index += 2) {
                short sample = (short)((buffer[index + 1] << 8) |
                                        buffer[index + 0]);
                // to floating point
                var sample32 = (sample / (float) short.MaxValue) * 5;
                // absolute value
                if (sample32 < 0) sample32 = -sample32;
                // is this the max value?
                if (sample32 > max) max = sample32;
                
                if(!CalculateTotalVolumePeak && max > threshold) break;
            }
            if (max > 1) return 1;
            return max;
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
