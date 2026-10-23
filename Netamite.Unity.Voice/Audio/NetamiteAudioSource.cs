using System;
using System.Collections.Generic;
using UnityEngine;

namespace Netamite.Unity.Voice.Audio {
    public class NetamiteAudioSource : MonoBehaviour {

        private UnityVoiceClient voiceClient;

        private bool muted = false;
        private float volume = 1f;
        private bool proximity = true;

        private AudioSource audioSource = null;
        public AudioSource AudioSource {
            get {
                if (audioSource == null) {
                    audioSource = gameObject.AddComponent<AudioSource>();

                    UpdateAudioSource();
                }

                return audioSource;
            }
        }

        private bool playing = false;

        public void Initialize(UnityVoiceClient voiceClient) {
            this.voiceClient = voiceClient;
        }

        public void Start() {
            playing = true;
            
            if (audioSource == null) return;
            if (audioSource.clip == null) return;

            audioSource.UnPause();
        }

        public void Stop() {
            playing = false;
            
            if (audioSource == null) return;
            if (audioSource.clip == null) return;

            audioSource.Pause();
        }


        public void QueuePlay(byte[] bytes) {
            if (!playing) return;
            
            PlayAudio(bytes);
        }
        
        public bool GetMute() {
            return this.muted;
        }

        public void SetMute(bool mute) {
            this.muted = mute;
            UpdateAudioSource();
        }

        public float GetVolume() {
            return this.volume;
        }
        
        public void SetVolume(float volume) {
            this.volume = volume;
            UpdateAudioSource();
        }
        
        public void SetProximity(bool proximity) {
            this.proximity = proximity;
            UpdateAudioSource();
        }
        
        
        /// <summary>
        /// Sample rate of the decoded voice data
        /// </summary>
        public int SampleRate = 16000;

        /// <summary>
        /// How much audio (ms) gets buffered before playback starts, absorbs network jitter. Higher = smoother but more delay
        /// </summary>
        public int JitterBufferMs = 60;

        /// <summary>
        /// Most audio (ms) that may be queued. When more piles up (network bursts, clock drift), the oldest audio gets
        /// dropped so the delay can't grow over time
        /// </summary>
        public int MaxBufferMs = 200;

        /// <summary>
        /// Gain applied to the voice before playback
        /// </summary>
        public float Gain = 10f;

        internal Queue<float> audioBuffer = new Queue<float>();

        public void UpdateAudioSource() {
            AudioSource.volume = muted ? 0 : this.volume;

            if (this.proximity) {
                AudioSource.spatialize = true;
                AudioSource.spatialBlend = 1.0f;
                AudioSource.minDistance = 5f;
                AudioSource.maxDistance = 50f;
                AudioSource.rolloffMode = AudioRolloffMode.Linear;
            } else {
                AudioSource.spatialize = false;
                AudioSource.spatialBlend = 0f;
            }
        }

        internal void PlayAudio(byte[] voiceData) {
            if (outputSampleRate == 0) outputSampleRate = AudioSettings.outputSampleRate; // Not accessible from the audio thread

            int maxSamples = SampleRate * MaxBufferMs / 1000;

            lock (audioBuffer) {
                for (int i = 0; i + 1 < voiceData.Length; i += 2) {
                    short sample = (short)(voiceData[i] | (voiceData[i + 1] << 8));
                    audioBuffer.Enqueue(sample / 32768.0f);
                }

                while (audioBuffer.Count > maxSamples) audioBuffer.Dequeue();
            }

            if (AudioSource.clip == null) {
                // Constant 1.0 carrier, Unity applies volume and 3D panning/attenuation to it before OnAudioFilterRead,
                // multiplying the voice onto it keeps proximity working
                AudioClip carrier = AudioClip.Create("VoiceCarrier", outputSampleRate, 1, outputSampleRate, false);
                float[] ones = new float[outputSampleRate];
                for (int i = 0; i < ones.Length; i++) ones[i] = 1f;
                carrier.SetData(ones, 0);

                AudioSource.loop = true;
                AudioSource.clip = carrier;
            }

            if (!AudioSource.isPlaying)
                AudioSource.Play();
        }

        private int outputSampleRate = 0;
        private bool buffering = true;
        private float resamplePosition = 0f; // Fraction between previousSample and nextSample
        private float previousSample = 0f;
        private float nextSample = 0f;

        // Runs on the audio thread for every block the output device needs, so playback only lags by one DSP buffer
        private void OnAudioFilterRead(float[] data, int channels) {
            if (outputSampleRate == 0) {
                Array.Clear(data, 0, data.Length);
                return;
            }

            float step = (float) SampleRate / outputSampleRate;
            int jitterSamples = SampleRate * JitterBufferMs / 1000;
            float levelScale = muted ? 0f : volume;
            float blockPeak = 0f;
            float blockSquares = 0f;
            int frames = data.Length / channels;

            lock (audioBuffer) {
                if (buffering && audioBuffer.Count >= jitterSamples) buffering = false;

                for (int i = 0; i < data.Length; i += channels) {
                    float value = 0f;

                    if (!buffering) {
                        // Linear interpolation from the voice sample rate to the output sample rate
                        resamplePosition += step;
                        while (resamplePosition >= 1f) {
                            resamplePosition -= 1f;
                            previousSample = nextSample;

                            if (audioBuffer.Count > 0) {
                                nextSample = audioBuffer.Dequeue();
                            } else {
                                // Ran dry, wait for the jitter buffer to fill up again instead of crackling
                                nextSample = 0f;
                                buffering = true;
                            }
                        }
                        value = previousSample + (nextSample - previousSample) * resamplePosition;
                        value = Mathf.Clamp(value * Gain, -1f, 1f);
                    }

                    float level = Math.Abs(value) * levelScale;
                    if (level > blockPeak) blockPeak = level;
                    blockSquares += level * level;

                    for (int c = 0; c < channels; c++) data[i + c] *= value;
                }
            }

            currentPeak = blockPeak;
            currentRms = frames > 0 ? (float) Math.Sqrt(blockSquares / frames) : 0f;
        }
        
        // Measured in OnAudioFilterRead over the last audio block. AudioSource.GetOutputData can't be used, it only
        // sees the constant carrier clip, not the voice that gets multiplied onto it
        private volatile float currentPeak = 0f;
        private volatile float currentRms = 0f;

        /// <summary>
        /// Peak level (0-1) of the voice that is currently playing, including volume and mute but without 3D attenuation
        /// </summary>
        public float GetPeak() {
            if (audioSource == null || !audioSource.isPlaying) return 0f;
            return currentPeak;
        }
        
        // RMS (average loudness) jitters less than the peak, usually looks better for lip sync
        /// <summary>
        /// RMS level (0-1) of the voice that is currently playing, including volume and mute but without 3D attenuation
        /// </summary>
        public float GetRms() {
            if (audioSource == null || !audioSource.isPlaying) return 0f;
            return currentRms;
        }
    }
}
