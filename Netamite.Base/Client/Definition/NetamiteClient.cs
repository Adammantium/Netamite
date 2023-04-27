using Netamite.Datatype;
using Netamite.Helper;
using Netamite.Network.Packet;
using Netamite.Network.Packet.Implementations;
using Netamite.Network.Processor;
using Netamite.Security;
using System;
using System.Threading;

namespace Netamite.Client.Definition {
    public class NetamiteClient : PacketProcessor {

        public int ClientId = 0;

        public virtual string TYPE => "NONE";

        internal string _STATE = "UNSET";
        public virtual string STATE {
            get { return _STATE; }
            set { _STATE = value; }
        }

        public bool IsConnected = false;

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

        /// <summary>
        /// How often a ping packet will be sent to the server.
        /// Values equal or lower than 0 will disable the automatic pings and can cause timeouts.
        /// </summary>
        private int _PingDelay = 1000;
        public int PingDelay {
            get { return _PingDelay; }
            set { _PingDelay = value; }
        }


        internal long lastServerTimestamp = 0;
        internal long serverTimestampTime = 0;
        internal long serverTimestampOffset = 0;
        /// <summary>
        /// Offset from the local time to the server in miliseconds.
        /// If the server has a missmatching time that mismatching with
        /// the client time, this will fix it.
        /// </summary>
        public long ServerTimeOffset {
            get { return serverTimestampOffset; }
        }

        /// <summary>
        /// Last ping messurement
        /// </summary>
        internal long _Ping;
        public long Ping {
            get { return _Ping; } 
        }

        /// <summary>
        /// This will return the fixed timestamp, this will take the server time
        /// and ping in account.
        /// If you want to have a central timestamp for all clients to use for clientside
        /// prediction, you should use this.
        /// It's a UNIX Timestamp.
        /// </summary>
        public long FixedTimestamp {
            get {
                return TimeHelper.Millis + ServerTimeOffset;
            }
        }

        public Action<NetPacket> OnDataReceived;

        public Action OnConnect;
        public Action<string> OnConnectionError;
        public Action<string> OnDisconnect;
        public Action<string> OnWarning;

        public NetamiteClient() {
            NetPacket.BuildPacketImplementationCache();
            if(Netamite.MasterClient == null) {
                Netamite.MasterClient = this;
            }
        }

        public virtual void Connect(string accessToken = "") {
            if(IsConnected) {
                Disconnect();
            }
            _STATE = "CONNECTING";
            ClientId = 0;
            ConnectToken = accessToken;
        }

        public virtual void Disconnect(string reason = DisconnectReason.CONNECTION_CLOSED) {
            if(IsConnected) {
                IsConnected = false;
                OnDataReceived = null;

                if(reason != null) SendReliable(new DisconnectPacket(ClientId, reason));

                OnDisconnect.Invoke(reason);
            }
            StopCheckingThreads();

            _STATE = "CLOSED";

            if(Netamite.MasterClient == this) {
                Netamite.MasterClient = null;
            }
        }

        public void Send(NetPacket packet) {
            if(packet.IsReliable()) {
                SendReliable(packet);
            } else {
                SendUnreliable(packet);
            }
        }
        public virtual void SendReliable(NetPacket packet) { reliablePacketsSent++; }
        public virtual void SendUnreliable(NetPacket packet) { unreliablePacketsSent++; }

        public void OnReliableReceived(NetPacket packet) {
            DataHandler(packet);
            reliablePacketsReceive++;    
        }
        public void OnUnreliableReceived(NetPacket packet) {
            DataHandler(packet);
            unreliablePacketsReceive++;
        }

        public virtual void InvokeOnConnect() {
            OnConnect?.Invoke();
        }

        public void EstablishConnection() {
            while(STATE.Equals("CONNECTING")) {
                SendReliable(new EstablishConnectionPacket(_ClientName, _ConnectToken));
                Thread.Sleep(1000);
            }
        }

        public void AwaitingConnectionHandler(NetPacket packet) {
            if(packet is WelcomePacket) {
                if(!IsConnected) {
                    OnDataReceived -= AwaitingConnectionHandler;

                    ClientId = ((WelcomePacket) packet).ClientId;

                    _STATE = "CONNECTED";
                    IsConnected = true;
                    
                    StartCheckingThreads();

                    InvokeOnConnect();
                }
            } else if(packet is ErrorPacket) {
                Disconnect();
                OnConnectionError?.Invoke(((ErrorPacket) packet).message);
            }
        }

        public void DataHandler(NetPacket packet) {
            if(!IsConnected) return;

            // Check if the Packet has a built in Implementation
            if(!ProcessPacket(packet, this)) {
                // If there is no built in Implementation, just continue
                OnDataReceived?.Invoke(packet);
            }
        }
        private Thread pingChecker = null;
        private CancellationTokenSource additionalServerThreadsCancellation = new CancellationTokenSource();
        private void StartCheckingThreads() {
            if(PingDelay > 0) {
                pingChecker = new Thread(PingThread) { IsBackground = true };
                pingChecker.Start();
            }
        }
        private void StopCheckingThreads() {
            additionalServerThreadsCancellation.Cancel();
            if(pingChecker != null) {
                pingChecker.Abort();
                pingChecker = null;
            }
        }
        private void PingThread() {
            while(!additionalServerThreadsCancellation.IsCancellationRequested && PingDelay > 0) {
                Send(new PingPacket());

                Thread.Sleep(PingDelay);
            }
        }
    }
}
