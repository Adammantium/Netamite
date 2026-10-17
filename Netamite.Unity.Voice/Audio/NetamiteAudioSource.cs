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
            float[] floatData = new float[voiceData.Length / 2];
            for (int i = 0; i < floatData.Length; i++) {
                short sample = (short)(voiceData[i * 2] | (voiceData[i * 2 + 1] << 8));
                floatData[i] = sample / 32768.0f;
            }

            lock (audioBuffer) {
                foreach (var sample in floatData) {
                    audioBuffer.Enqueue(sample);
                }
            }
            
            if (AudioSource.clip == null) {
                AudioSource.loop = true;
                AudioSource.clip = AudioClip.Create("StreamingClip", 16000 * 2, 1, 16000, true, OnAudioFilterRead);
            }
            
            if (!AudioSource.isPlaying)
                AudioSource.Play();
        }
        
        void OnAudioFilterRead(float[] data) {
            lock (audioBuffer) {
                for (int i = 0; i < data.Length; i++) {
                    if (audioBuffer.Count > 0) {
                        data[i] = Math.Min(1, audioBuffer.Dequeue() * 10f);
                    } else {
                        data[i] = 0f; // Fill with silence if buffer is empty
                    }
                }
            }
        }
        
        private readonly float[] samples = new float[256];
        public float GetPeak() {
            if (AudioSource == null || !AudioSource.isPlaying) return 0f;

            float peak = 0f;
            for (int channel = 0; channel < AudioSource.clip.channels; channel++) {
                AudioSource.GetOutputData(samples, channel);
                for (int i = 0; i < samples.Length; i++) {
                    float abs = Mathf.Abs(samples[i]);
                    if (abs > peak) peak = abs;
                }
            }
            return peak;
        }
        
        // RMS (average loudness) jitters less than the peak, usually looks better for lip sync
        public float GetRms() {
            if (AudioSource == null || !AudioSource.isPlaying) return 0f;

            AudioSource.GetOutputData(samples, 0);
            float sum = 0f;
            for (int i = 0; i < samples.Length; i++) sum += samples[i] * samples[i];
            return Mathf.Sqrt(sum / samples.Length);
        }
    }
}
