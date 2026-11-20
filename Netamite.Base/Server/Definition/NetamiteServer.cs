using Netamite.Datatype;
using Netamite.Helper;
using Netamite.Logging;
using Netamite.Network.Packet;
using Netamite.Network.Packet.Implementations;
using Netamite.Network.Processor;
using Netamite.Security;
using Netamite.Server.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Netamite.Server.Definition {
    public class NetamiteServer : PacketProcessor {

        public ConcurrentDictionary<int, ClientInformation> _clients = new ConcurrentDictionary<int, ClientInformation>();
        public ClientInformation[] Clients {
            get { return _clients.Values.ToArray(); }
        }
        public int ConnectedClients {
            get { return _clients.Count; }
        }

        private int _MaxClients = int.MaxValue;
        /// <summary>
        /// Maximum of concurrent connections the server will allow
        /// </summary>
        public int MaxClients { 
            get { return _MaxClients; }
            set { _MaxClients = value; }
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

        private bool _IsRunning;
        public bool IsRunning { get { return _IsRunning; } }

        /// <summary>
        /// Delay after what the client times out and has probably lost connection.
        /// Values equal or lower than 0 will disable the timeout checks.
        /// </summary>
        private long _ClientTimeoutDelay = 30000;
        public long ClientTimeoutDelay {
            get { return _ClientTimeoutDelay; }
            set {  _ClientTimeoutDelay = value; }
        }

        /// <summary>
        /// Gets called right before OnStart
        /// You can use this to prepare some data before all the OnStart Events are called
        /// </summary>
        public Action<long> BeforeStart;
        /// <summary>
        /// Gets called once the server is ready
        /// </summary>
        public Action<long> OnStart;
        /// <summary>
        /// Gets called when the Startup fails
        /// </summary>
        public Action<string> OnStartupError;
        /// <summary>
        /// Gets called right before OnStop
        /// You can use this to prepare some data before all the OnStop Events are called
        /// </summary>
        public Action BeforeStop;
        /// <summary>
        /// Gets called once the server is stopped
        /// </summary>
        public Action OnStop;
        public Action<ClientInformation> OnConnect;
        public Action<ClientInformation, string> OnDisconnect;
        public Action<ClientInformation, NetPacket> OnDataReceived;
        public Action<ClientInformation, NetPacket> OnDataSent;


        public NetamiteServer() {
            Netamite.Initialize();
            NetPacket.BuildPacketImplementationCache();
            if(Netamite.MasterServer == null) {
                Netamite.MasterServer = this;
            }
        }

        public NetamiteServer(int maxClients) : base() {
            this.MaxClients = maxClients;
        }


        internal string CheckEstablishConnection(EstablishConnectionPacket ecp) {
            if(HasConnectToken) {
                if(!_ConnectToken.Equals(ecp.password)) { // Passwords are hashed with SHA256
                    return $"WRONG_PASSWORD";
                }
            }

            if(_clients.Count >= MaxClients) {
                return "SERVER_FULL";
            }

            return null;
        }

        internal int currentId = 0;
        public int NextId { get { return Interlocked.Increment(ref currentId); } }
        public virtual void ClientJoined(ClientInformation clientInformation) {
            if(clientInformation.ClientId <= 0) {
                clientInformation.ClientId = Interlocked.Increment(ref currentId);
            }
            clientInformation.ConnectionTimestamp = DateTime.Now;
            _clients.TryAdd(clientInformation.ClientId, clientInformation);

            SendTo(clientInformation, new WelcomePacket(clientInformation.ClientId));

            InitializeTimeSync(clientInformation);

            OnConnect?.Invoke(clientInformation);
        }

        public virtual void ClientLeft(ClientInformation client, string reason = DisconnectReason.CONNECTION_CLOSED) {
            if(_clients.ContainsKey(client.ClientId)) {
                SendTo(client, new DisconnectPacket(client.ClientId, reason));
                SendToAllExcept(new DisconnectPacket(client.ClientId, reason), client.ClientId);
                
                _clients.TryRemove(client.ClientId, out _);

                OnDisconnect?.Invoke(client, reason);
            }
        }

        public void ProcessPacket(ClientInformation client, NetPacket packet) {
            if(packet == null) return;
            if(!_clients.ContainsKey(client.ClientId)) return;

            client.lastPacketTimestamp = TimeHelper.Millis; // Remember when we received the last packet to not timeout the player

            // Check if the Packet has a built in Implementation
            if(!ProcessPacket(packet, this, client)) {
                // If there is no built in Implementation, just continue
                OnDataReceived?.Invoke(client, packet);
            }
        }

        public virtual void Start() { }
        public virtual void Stop() {
            InvokeStopEvent(false);

            InvokeStopEvent(true);
        }

        public void InvokeStopEvent(bool stopped) {
            if(!stopped) {
                foreach(ClientInformation client in Clients) SendTo(client, new DisconnectPacket(client.ClientId, DisconnectReason.SERVER_CLOSED));
            } else {
                _IsRunning = false;
                if(Netamite.MasterServer == this) {
                    Netamite.MasterServer = null;
                }
                StopCheckingThreads();
                BeforeStop?.Invoke();
                OnStop?.Invoke();
            }
        }

        private long startDelay = 0;
        public void InvokeStartEvent(bool started) {
            if(!started) {
                startDelay = TimeHelper.Millis;
            } else {
                long duration = TimeHelper.Millis - startDelay;
                StartCheckingThreads();
                _IsRunning = true;
                BeforeStart?.Invoke(duration);
                OnStart?.Invoke(duration);
            }
        }

        public void SendToAll(NetPacket p) {
            if(p.IsReliable())
                SendReliableToAll(p);
            else
                SendUnreliableToAll(p);
        }

        public void SendToAllExcept(NetPacket p, params int[] exceptions) {
            if(p.IsReliable())
                SendReliableToAllExcept(p, exceptions);
            else
                SendUnreliableToAllExcept(p, exceptions);
        }

        public void SendTo(int clientId, NetPacket p) {
            ClientInformation client;
            if(!_clients.TryGetValue(clientId, out client)) return;

            SendTo(client, p);
        }

        private static bool IsException(int clientId, int[] exceptions) {
            if(exceptions == null) return false;
            for(int i = 0; i < exceptions.Length; i++) {
                if(exceptions[i] == clientId) return true;
            }
            return false;
        }

        public void SendTo(ClientInformation client, NetPacket p) {
            if(p.IsReliable())
                SendReliableTo(client, p);
            else
                SendUnreliableTo(client, p);
        }

        // Reliable
        public void SendReliableToAll(NetPacket p) {
            SendReliableToAllExcept(p);
        }

        public void SendReliableToAllExcept(NetPacket p, params int[] exceptions) {
            // The packet is the same for everybody, so it only gets serialized once and not once per client
            PreparedPacket prepared = new PreparedPacket(p);

            foreach(KeyValuePair<int, ClientInformation> entry in _clients) {
                if(IsException(entry.Key, exceptions)) continue;

                SendReliableTo(entry.Value, prepared);
            }
        }

        public virtual void SendReliableTo(ClientInformation client, NetPacket p) { }

        /// <summary>
        /// Sends a packet that goes out to multiple clients. Transports override this to send p.GetData(), which is only
        /// serialized once. Without an override the packet is sent like any other and serialized for every client.
        /// </summary>
        public virtual void SendReliableTo(ClientInformation client, PreparedPacket p) {
            SendReliableTo(client, p.Packet);
        }


        // Unreliable
        public void SendUnreliableToAll(NetPacket p) {
            SendUnreliableToAllExcept(p);
        }

        public void SendUnreliableToAllExcept(NetPacket p, params int[] exceptions) {
            // The packet is the same for everybody, so it only gets serialized once and not once per client
            PreparedPacket prepared = new PreparedPacket(p);

            foreach(KeyValuePair<int, ClientInformation> entry in _clients) {
                if(IsException(entry.Key, exceptions)) continue;

                SendUnreliableTo(entry.Value, prepared);
            }
        }

        public virtual void SendUnreliableTo(ClientInformation client, NetPacket p) { }

        /// <summary>
        /// Sends a packet that goes out to multiple clients. Transports override this to send p.GetData(), which is only
        /// serialized once. Without an override the packet is sent like any other and serialized for every client.
        /// </summary>
        public virtual void SendUnreliableTo(ClientInformation client, PreparedPacket p) {
            SendUnreliableTo(client, p.Packet);
        }


        /// <summary>
        /// Initialized a Time-Sync to all clients, to messure the time offset between client and server
        /// </summary>
        public void InitializeTimeSync() {
            foreach(ClientInformation client in _clients.Values) {
                if(client.nextTimeSyncStamp > TimeHelper.Millis) return;

                InitializeTimeSync(client);
            }
        }

        /// <summary>
        /// Initialized a Time-Sync to the client, to messure the time offset between client and server
        /// </summary>
        /// <param name="client">The Client to initialize the time sync to</param>
        public void InitializeTimeSync(ClientInformation client) {
            TimeSynchronizationPacket timeSynchronizationPacket = new TimeSynchronizationPacket(); // UNIX Timestamp will automatically be set
            client.lastTimeSyncStamp = timeSynchronizationPacket.server_timestamp;

            if(client.secondDelayBetweenTimeSync < 10) {
                client.secondDelayBetweenTimeSync++;
            }else if(client.secondDelayBetweenTimeSync < 60) {
                client.secondDelayBetweenTimeSync += 10;
            }
            client.nextTimeSyncStamp = TimeHelper.Millis + (client.secondDelayBetweenTimeSync * 1000);

            SendTo(client, timeSynchronizationPacket);
        }

        private Thread timeoutCheckerThread = null;
        private Thread timeSyncThread = null;
        private CancellationTokenSource additionalServerThreadsCancellation = new CancellationTokenSource();
        private void StartCheckingThreads() {
            if(ClientTimeoutDelay > 0) {
                timeoutCheckerThread = new Thread(TimeoutCheckThread) { IsBackground = true };
                timeoutCheckerThread.Start();
            }
            timeSyncThread = new Thread(TimeSyncThread) { IsBackground = true };
            timeSyncThread.Start();
        }
        private void StopCheckingThreads() {
            additionalServerThreadsCancellation.Cancel();
            if(timeoutCheckerThread != null) {
                timeoutCheckerThread.Abort();
                timeoutCheckerThread = null;
            }
            if(timeSyncThread != null) {
                timeSyncThread.Abort();
                timeSyncThread = null;
            }
        }
        private void TimeoutCheckThread() {
            while(!additionalServerThreadsCancellation.IsCancellationRequested) {
                long timestamp = TimeHelper.Millis;
                foreach(ClientInformation clientInformation in _clients.Values) {
                    if(timestamp - clientInformation.lastPacketTimestamp > ClientTimeoutDelay) {
                        ClientLeft(clientInformation, DisconnectReason.CONNECTION_TIMEOUT);
                    }
                }
                Thread.Sleep(1000);
            }
        }
        private void TimeSyncThread() {
            while(!additionalServerThreadsCancellation.IsCancellationRequested) {
                InitializeTimeSync();
                Thread.Sleep(1000);
            }
        }

        public ClientInformation GetClientById(int id) {
            ClientInformation client;
            return _clients.TryGetValue(id, out client) ? client : null;
        }
    }
}
