using NAudio.Wave;
using Netamite.Client.Definition;
using Netamite.Logging;
using Netamite.Voice.Audio;
using Netamite.Voice.Codec;
using Netamite.Voice.Packet;
using System.Collections.Generic;

namespace Netamite.Voice {
    public class VoiceClient {

        internal static VoiceClient instance;

        private VoiceReader reader;

        internal NetamiteClient client;


        public AudioCodec Codec { get; }

        public WaveFormat Format {
            get { return Codec.RecordFormat; }
        }

        public bool UseMic = true;
        public bool UseOutput = true;

        /// <summary>
        /// Wished latency in ms
        /// </summary>
        public int Latency = 100;

        private IDictionary<int, AudioSource> audioPlayers = new Dictionary<int, AudioSource>();

        public VoiceClient(NetamiteClient client, AudioCodec audioCodec = null) {
            if(instance != null) {
                instance.Stop();
            }

            instance = this;
            this.client = client;
            this.Codec = audioCodec == null ? new G722Codec() : audioCodec;

            reader = new VoiceReader(this);

            reader.OnAudioPacketReady += OnPacketReady;
        }

        public void Start() {
            if(UseMic) reader.Start();
        }

        public void Stop() {
            reader.Stop();
            
            foreach(AudioSource player in audioPlayers.Values) {
                player.Stop();
            }
        }

        internal void OnData(int clientId, byte[] voiceData) {
            if(!UseOutput) return;
            
            CheckClient(clientId);

            audioPlayers[clientId]?.QueuePlay(voiceData);
        }

        internal void OnPacketReady(AudioPacket packet) {
            client?.Send(packet);
        }

        private void CheckClient(int clientId) {
            if(!audioPlayers.ContainsKey(clientId)) {
                AudioSource player = new AudioSource(this);
                player.Start();
                audioPlayers.Add(clientId, player);
            }
        }

        public void SetClientVolume(int clientId, float volume) {
            CheckClient(clientId);

            audioPlayers[clientId].SetVolume(volume);
        }

        public static Dictionary<int, string> GetInputDevices() {
            Dictionary<int, string> devices = new Dictionary<int, string>();
            int waveInDevices = WaveInEvent.DeviceCount;
            for(int waveInDevice = 0; waveInDevice < waveInDevices; waveInDevice++) {
                WaveInCapabilities deviceInfo = WaveInEvent.GetCapabilities(waveInDevice);
                devices.Add(waveInDevice, deviceInfo.ProductName);
            }
            return devices;
        }

        public void SetInputDevice(int deviceId) {
            reader?.SetDevice(deviceId);
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
