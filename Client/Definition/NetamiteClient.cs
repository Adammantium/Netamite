using Netamite.Logging;
using Netamite.Network.Packet;
using Netamite.Network.Packet.Implementations;
using Netamite.Network.Processor;
using Netamite.Security;
using System;
using System.Threading;

namespace Netamite.Client.Definition {
    public class NetamiteClient : PacketProcessor {

        internal ulong ClientId = 0;

        public virtual string TYPE => "NONE";

        internal string _STATE = "UNSET";
        public virtual string STATE { get { return _STATE; } }

        public bool isConnected = false;

        private string _ClientName = "UNNAMED";
        public string ClientName { 
            get { return _ClientName; }
            set { _ClientName = value; }
        }

        private string _ConnectToken = string.Empty;
        /// <summary>
        /// Sets the connection token every client has to provide.
        /// It will automatically get hashed with SHA256 when supplied this way.
        /// </summary>
        public string ConnectToken {
            set {
                if(String.IsNullOrEmpty(value))
                    _ConnectToken = string.Empty;
                else
                    _ConnectToken = Hashing.SHA256(value);
            }
        }
        public bool HasConnectToken {
            get { return _ConnectToken != null && _ConnectToken.Length > 0; }
        }

        internal ulong unreliableSent = 0;
        internal ulong reliableSent = 0;
        internal ulong unreliableReceive = 0;
        internal ulong reliableReceive = 0;

        internal long lastServerTimestamp = 0;
        internal long serverTimestampTime = 0;
        internal long serverTimestampOffset = 0;
        public long ServerTimeOffset {
            get { return serverTimestampOffset; }
        }

        internal long _Ping;
        public long Ping {
            get { return _Ping; } 
        }

        public Action<NetPacket> OnDataReceived;

        public Action onConnect;
        public Action<string> onConnectionError;
        public Action onDisconnect;

        public NetamiteClient() {
            NetPacket.BuildPacketImplementationCache();
        }

        internal ulong GetBytesSent() {
            ulong bytesSent = unreliableSent + reliableSent;

            unreliableSent = 0;
            reliableSent = 0;

            return bytesSent;
        }

        internal ulong GetBytesReceive() {
            ulong bytesReceive = unreliableReceive + reliableReceive;

            unreliableReceive = 0;
            reliableReceive = 0;

            return bytesReceive;
        }

        public virtual void Connect(string accessToken = "") {
            if(isConnected) {
                Disconnect();
            }
            _STATE = "CONNECTING";
            ClientId = 0;
            ConnectToken = accessToken;
        }

        public virtual void Disconnect() {
            if(isConnected) {
                isConnected = false;
                OnDataReceived = null;
                onDisconnect.Invoke();
            }
            _STATE = "CLOSED";
        }

        public void Send(NetPacket packet) {
            if(packet.IsReliable()) {
                SendReliable(packet);
            } else {
                SendUnreliable(packet);
            }
        }
        public virtual void SendReliable(NetPacket packet) { reliableSent++; }
        public virtual void SendUnreliable(NetPacket packet) { unreliableSent++; }

        public virtual void OnReliableReceived(NetPacket packet) {
            DataHandler(packet);
            reliableReceive++;    
        }
        public virtual void OnUnreliableReceived(NetPacket packet) {
            DataHandler(packet);
            unreliableReceive++;
        }

        public void EstablishConnection() {
            while(STATE.Equals("CONNECTING")) {
                SendReliable(new EstablishConnectionPacket(_ClientName, _ConnectToken));
                Thread.Sleep(1000);
            }
        }

        public void AwaitingConnectionHandler(NetPacket packet) {
            if(packet is WelcomePacket) {
                if(!isConnected) {
                    OnDataReceived -= AwaitingConnectionHandler;

                    ClientId = ((WelcomePacket) packet).ClientId;

                    _STATE = "CONNECTED";
                    isConnected = true;

                    onConnect?.Invoke();
                }
            } else if(packet is ErrorPacket) {
                Disconnect();
                onConnectionError?.Invoke(((ErrorPacket) packet).message);
            }
        }

        public void DataHandler(NetPacket packet) {
            // Check if the Packet has a built in Implementation
            if(!ProcessPacket(packet, this)) {
                // If there is no built in Implementation, just continue
                OnDataReceived?.Invoke(packet);
            }
        }
    }
}
