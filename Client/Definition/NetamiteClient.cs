using Netamite.Logging;
using Netamite.Network.Packet;
using Netamite.Network.Packet.Implementations;
using System;

namespace Netamite.Client.Definition {
    public class NetamiteClient {

        internal ulong ClientId = 0;

        public virtual string TYPE => "NONE";

        internal string _STATE = "UNSET";
        public virtual string STATE { get { return _STATE; } }

        public bool isConnected = false;

        internal long unreliableSent = 0;
        internal long reliableSent = 0;
        internal long unreliableReceive = 0;
        internal long reliableReceive = 0;

        public Action<NetPacket> OnPacketReceived;

        public Action onConnect;
        public Action onDisconnect;

        internal long GetBytesSent() {
            long bytesSent = unreliableSent + reliableSent;

            unreliableSent = 0;
            reliableSent = 0;

            return bytesSent;
        }

        internal long GetBytesReceive() {
            long bytesReceive = unreliableReceive + reliableReceive;

            unreliableReceive = 0;
            reliableReceive = 0;

            return bytesReceive;
        }

        public virtual void Connect(string token = "") { }

        public virtual void Disconnect() {
            isConnected = false;
            OnPacketReceived = null;
            onDisconnect.Invoke();
            _STATE = "CLOSED";
        }

        public virtual void SendReliable(NetPacket packet) { }
        public virtual void SendUnreliable(NetPacket packet) { }

        public void AwaitingConnectionHandler(NetPacket packet) {
            if(packet is WelcomePacket) {
                OnPacketReceived -= AwaitingConnectionHandler;
                OnPacketReceived += DataHandler;

                ClientId = ((WelcomePacket) packet).ClientId;

                _STATE = "CONNECTED";
                isConnected = true;

                onConnect?.Invoke();
            } else if(packet is ErrorPacket) {

            }
        }

        public void DataHandler(NetPacket packet) {

        }
    }
}
