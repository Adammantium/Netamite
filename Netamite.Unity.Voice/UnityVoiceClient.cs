using Netamite.Client.Definition;
using Netamite.Logging;
using Netamite.Unity.Voice.Codec;
using Netamite.Unity.Voice.Audio;
using Netamite.Unity.Voice.Packet;
using System.Collections.Generic;
using UnityEngine;
using Netamite.Voice.Codec;

namespace Netamite.Unity.Voice {
    public class UnityVoiceClient {

        internal static UnityVoiceClient instance;

        private VoiceReader reader;

        internal NetamiteClient client;


        public AudioCodec Codec { get; }

        public bool UseMic = true;
        public bool UseOutput = true;

        /// <summary>
        /// Wished latency in ms
        /// </summary>
        public int Latency = 50;

        private IDictionary<int, NetamiteAudioSource> audioPlayers = new Dictionary<int, NetamiteAudioSource>();

        public delegate void onAudioPacketReceived(int playerId, byte[] voiceData);
        public onAudioPacketReceived OnAudioPacketReceived;
        
        public UnityVoiceClient(NetamiteClient client, AudioCodec audioCodec = null, bool useExternalAudio = false) {
            if(instance != null) {
                instance.Stop();
            }

            instance = this;
            this.client = client;
            this.Codec = audioCodec == null ? new G722Codec() : audioCodec;

            GameObject voiceReaderObj = new GameObject("VoiceReader");
            reader = voiceReaderObj.AddComponent<VoiceReader>();
            reader.Initialize(this);
            reader.OnAudioPacketReady += OnPacketReady;
        }

        public void Start() {
            if(UseMic) reader.Start();
        }

        public void Stop() {
            reader.Stop();
            
            foreach(NetamiteAudioSource player in audioPlayers.Values) {
                player.Stop();
            }
        }

        internal void OnData(int clientId, byte[] voiceData) {
            if(!UseOutput) return;
            
            CheckClient(clientId);

            audioPlayers[clientId]?.QueuePlay(voiceData);
        }

        internal void OnPacketReady(UnityAudioPacket packet) {
            client?.Send(packet);
        }

        private void CheckClient(int clientId) {
            if(!audioPlayers.ContainsKey(clientId)) {
                GameObject gameObject = new GameObject("AudioSource" + clientId);
                NetamiteAudioSource player = gameObject.AddComponent<NetamiteAudioSource>();
                player.Start();
                audioPlayers.Add(clientId, player);
            }
        }
        
        public NetamiteAudioSource GetAudioPlayer(int clientId) {
            CheckClient(clientId);

            return audioPlayers[clientId];
        }
        
        public void SetClientVolume(int clientId, float volume) {
            CheckClient(clientId);

            audioPlayers[clientId].SetVolume(volume);
        }
        
        public void SetVolume(float volume) {
            foreach(var audioPlayer in audioPlayers.Values)
                audioPlayer.SetVolume(volume);
        }
        
        public void SetClientProximity(int clientId, bool proximity) {
            CheckClient(clientId);

            audioPlayers[clientId].SetProximity(proximity);
        }

        public void SetProximity(bool proximity) {
            foreach (var audioPlayer in audioPlayers.Values)
                audioPlayer.SetProximity(proximity);
        }

        public static Dictionary<int, string> GetInputDevices() {
            Dictionary<int, string> devices = new Dictionary<int, string>();
            
            return devices;
        }

        public void SetInputDevice(string deviceName) {
            reader?.SetDevice(deviceName);
        }

        internal int _OutputDeviceId = 0;
        public void SetOutputDevice(int deviceId) {
            _OutputDeviceId = deviceId;
            foreach(var player in audioPlayers.Values) {
                player?.SetDevice(deviceId);
            }
        }

        public void SetRecordingThreshold(float threshold) {
            if(reader != null)
                reader.MinVolumePeak = threshold;
        }
    }
}
