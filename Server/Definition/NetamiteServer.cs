using Netamite.Helper;
using Netamite.Network.Packet;
using Netamite.Network.Packet.Implementations;
using Netamite.Network.Processor;
using Netamite.Security;
using Netamite.Server.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Netamite.Server.Definition {
    public class NetamiteServer : PacketProcessor {

        public Dictionary<ulong, ClientInformation> _clients = new Dictionary<ulong, ClientInformation>();
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


        public Action<long> OnStart;
        public Action<ClientInformation> OnConnect;
        public Action<ClientInformation> OnDisconnect;
        public Action<ClientInformation, NetPacket> OnDataReceived;
        public Action<ClientInformation, NetPacket> OnDataSent;


        public NetamiteServer() {
            NetPacket.BuildPacketImplementationCache();
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

        internal static ulong currentId;
        internal virtual void ClientJoined(ClientInformation clientInformation) {
            if(clientInformation.ClientId <= 0) {
                clientInformation.ClientId = ++currentId;
            }
            clientInformation.ConnectionTimestamp = DateTime.Now;
            _clients.Add(clientInformation.ClientId, clientInformation);

            SendReliableTo(clientInformation, new WelcomePacket(clientInformation.ClientId));

            InitializeTimeSync(clientInformation);

            OnConnect?.Invoke(clientInformation);
        }

        internal virtual void ClientLeft(ClientInformation clientInformation) {
            _clients.Remove(clientInformation.ClientId);

            OnDisconnect?.Invoke(clientInformation);
        }

        internal void ProcessPacket(ClientInformation client, NetPacket packet) {
            // Check if the Packet has a built in Implementation
            if(!ProcessPacket(packet, this, client)) {
                // If there is no built in Implementation, just continue
                OnDataReceived?.Invoke(client, packet);
            }
        }

        /// <summary>
        /// This function should not be called.
        /// Please use the function on a super class.
        /// </summary>
        public virtual void Start() { }

        private long startDelay = 0;
        internal void Start(bool start) {
            if(start) {
                startDelay = TimeHelper.Millis;
            } else {
                long length = TimeHelper.Millis - startDelay;
                OnStart.Invoke(length);
            }
        }

        public void SendToAll(NetPacket p) {
            if(p.IsReliable())
                SendReliableToAll(p);
            else
                SendUnreliableToAll(p);
        }

        public void SendToAllExcept(NetPacket p, params ulong[] exceptions) {
            if(p.IsReliable())
                SendReliableToAllExcept(p, exceptions);
            else
                SendUnreliableToAllExcept(p, exceptions);
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

        public void SendReliableToAllExcept(NetPacket p, params ulong[] exceptions) {
            foreach(ClientInformation client in Clients) {
                if(exceptions.Contains(client.ClientId)) continue;

                SendReliableTo(client, p);
            }
        }

        public virtual void SendReliableTo(ClientInformation client, NetPacket p) { }


        // Unreliable
        public void SendUnreliableToAll(NetPacket p) {
            SendUnreliableToAllExcept(p);
        }

        public void SendUnreliableToAllExcept(NetPacket p, params ulong[] exceptions) {
            foreach(ClientInformation client in Clients) {
                if(exceptions.Contains(client.ClientId)) continue;

                SendUnreliableTo(client, p);
            }
        }

        public virtual void SendUnreliableTo(ClientInformation client, NetPacket p) { }


        /// <summary>
        /// Initialized a Time-Sync to all clients, to messure the time offset between client and server
        /// </summary>
        private void InitializeTimeSync() {
            foreach(ClientInformation client in _clients.Values) {
                InitializeTimeSync(client);
            }
        }

        /// <summary>
        /// Initialized a Time-Sync to the client, to messure the time offset between client and server
        /// </summary>
        /// <param name="client">The Client to initialize the time sync to</param>
        private void InitializeTimeSync(ClientInformation client) {
            TimeSynchronizationPacket timeSynchronizationPacket = new TimeSynchronizationPacket(); // UNIX Timestamp will automatically be set
            client.lastTimeSyncStamp = timeSynchronizationPacket.server_timestamp;

            SendReliableTo(client, timeSynchronizationPacket);
        }
    }
}
