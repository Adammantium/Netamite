using System;
using System.Collections.Generic;
using UnityEngine;

namespace Netamite.Unity.Voice.Audio {
    public class NetamiteAudioSource : MonoBehaviour {

        private UnityVoiceClient voiceClient;

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

        public void SetVolume(float volume) {
            this.volume = volume;
            UpdateAudioSource();
        }
        
        public void SetProximity(bool proximity) {
            this.proximity = proximity;
            UpdateAudioSource();
        }


        internal void SetDevice(int deviceId) {
            
        }
        
        
        
        internal Queue<float> audioBuffer = new Queue<float>();

        public void UpdateAudioSource() {
            audioSource.volume = this.volume;

            if (this.proximity) {
                audioSource.spatialize = true;
                audioSource.spatialBlend = 1.0f;
                audioSource.minDistance = 5f;
                audioSource.maxDistance = 50f;
                audioSource.rolloffMode = AudioRolloffMode.Linear;
            } else {
                audioSource.spatialize = false;
                audioSource.spatialBlend = 0f;
            }
        }

        internal void PlayAudio(byte[] voiceData) {
            if (AudioSource.clip == null) {
                audioSource.playOnAwake = true;
                audioSource.loop = true;
                audioSource.clip = AudioClip.Create("StreamingClip", 16000 * 10, 1, 16000, true, OnAudioFilterRead);
                audioSource.Play();
            }

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
    }
}
