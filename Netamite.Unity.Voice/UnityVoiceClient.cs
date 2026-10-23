using Netamite.Client.Definition;
using Netamite.Logging;
using Netamite.Voice.Codec;
using Netamite.Unity.Voice.Audio;
using Netamite.Unity.Voice.Packet;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Netamite.Unity.Voice {
    public class UnityVoiceClient {

        internal static UnityVoiceClient instance;

        private VoiceReader reader;

        internal NetamiteClient client;


        /// <summary>
        /// Codec used to encode the own voice. Every remote speaker gets their own decoder made with Codec.CreateNew()
        /// </summary>
        public AudioCodec Codec { get; }

        private readonly ConcurrentDictionary<int, AudioCodec> decoders = new ConcurrentDictionary<int, AudioCodec>();

        // Players that left. Packets still in flight are ignored, so they don't bring back the decoder or audio source
        private readonly ConcurrentDictionary<int, byte> removedClients = new ConcurrentDictionary<int, byte>();
        private readonly ConcurrentQueue<int> clientsToRemove = new ConcurrentQueue<int>();

        public bool UseMic = true;
        public bool UseOutput = true;

        /// <summary>
        /// Wished latency in ms
        /// </summary>
        public int Latency = 50;

        private IDictionary<int, NetamiteAudioSource> audioPlayers = new Dictionary<int, NetamiteAudioSource>();

        // Voice packets arrive on the network thread, but GameObjects and AudioSources may only be touched on the
        // main thread (IL2CPP crashes otherwise). Decoded audio waits here until the MainThreadPump plays it
        private readonly ConcurrentQueue<KeyValuePair<int, byte[]>> receivedAudio = new ConcurrentQueue<KeyValuePair<int, byte[]>>();

        public delegate void onAudioPacketReceived(int playerId, byte[] voiceData);
        public onAudioPacketReceived OnAudioPacketReceived;

        public delegate void onTalkingStateChanged(bool talking);
        public onTalkingStateChanged OnTalkingStateChanged;

        public UnityVoiceClient(NetamiteClient client, AudioCodec audioCodec = null, bool useExternalAudio = false) {
            if(instance != null) {
                instance.Stop();
            }

            instance = this;
            this.client = client;
            this.Codec = audioCodec ?? new G722Codec();
            
            GameObject voiceReaderObj = new GameObject("VoiceReader");
            reader = voiceReaderObj.AddComponent<VoiceReader>();
            reader.Initialize(this);
            reader.OnAudioPacketReady += OnPacketReady;

            voiceReaderObj.AddComponent<MainThreadPump>().voiceClient = this;
        }

        public void Start() {
            if(UseMic) reader.Start();
        }

        public void Stop() {
            reader.Stop();
            decoders.Clear();
            removedClients.Clear();
            
            foreach(NetamiteAudioSource player in audioPlayers.Values) {
                player.Stop();
            }
        }

        // Network thread: decode here (the expensive part), play on the main thread
        internal void OnData(int clientId, byte[] voiceData) {
            if(!UseOutput) return;
            if(removedClients.ContainsKey(clientId)) return;

            AudioCodec decoder = decoders.GetOrAdd(clientId, id => Codec.CreateNew());

            byte[] pcm;
            lock(decoder) { // Packets of one speaker could arrive on different threads (e.g. separate sockets)
                pcm = decoder.Decode(voiceData);
            }
            receivedAudio.Enqueue(new KeyValuePair<int, byte[]>(clientId, pcm));
        }

        // Main thread, every frame
        internal void PlayReceivedAudio() {
            while(receivedAudio.TryDequeue(out KeyValuePair<int, byte[]> audio)) {
                if(!UseOutput) continue;
                if(removedClients.ContainsKey(audio.Key)) continue;

                CheckClient(audio.Key);
                audioPlayers[audio.Key]?.QueuePlay(audio.Value);
            }

            while(clientsToRemove.TryDequeue(out int clientId)) {
                if(!audioPlayers.TryGetValue(clientId, out NetamiteAudioSource player)) continue;

                audioPlayers.Remove(clientId);
                if(player != null) UnityEngine.Object.Destroy(player.gameObject); // Unity null check, it might already be destroyed with its parent
            }
        }

        /// <summary>
        /// Call when a player left, frees their decoder and destroys their audio source. Can be called from any thread.
        /// Voice data from that client id is ignored afterwards (until Stop), as packets can still be on their way.
        /// </summary>
        public void RemoveClient(int clientId) {
            removedClients[clientId] = 0;
            decoders.TryRemove(clientId, out _);
            clientsToRemove.Enqueue(clientId);
        }

        internal void OnPacketReady(UnityAudioPacket packet) {
            client?.Send(packet);
        }

        private void CheckClient(int clientId) {
            if(audioPlayers.ContainsKey(clientId) && audioPlayers[clientId] == null) {
                audioPlayers.Remove(clientId);
            }

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
        
        public bool GetClientMute(int clientId) {
            CheckClient(clientId);
            
            return audioPlayers[clientId].GetMute();
        }

        public void SetClientMute(int clientId, bool mute) {
            CheckClient(clientId);
            
            audioPlayers[clientId].SetMute(mute);
        }
        
        public float GetClientVolume(int clientId) {
            CheckClient(clientId);

            return audioPlayers[clientId].GetVolume();
        }
        
        public void SetClientVolume(int clientId, float volume) {
            CheckClient(clientId);

            audioPlayers[clientId].SetVolume(volume);
        }
        
        public void SetVolume(float volume) {
            foreach (var audioPlayer in audioPlayers) {
                try {
                    audioPlayer.Value.SetVolume(volume);
                } catch (NullReferenceException) {
                    audioPlayers.Remove(audioPlayer.Key);
                }
            }
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
        
        public void SetRecordingThreshold(float threshold) {
            if(reader != null)
                reader.MinVolumePeak = threshold;
        }
    }
}
