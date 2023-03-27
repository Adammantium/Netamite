using Netamite.Network.Packet;
using Netamite.Network.Socket;
using Netamite.Security;
using Netamite.Server.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Netamite.Server.Definition {
    public class NetamiteServer {

        public List<ClientInformation> _clients = new List<ClientInformation>();
        public ClientInformation[] Clients {
            get { return _clients.ToArray(); }
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
            set { _ConnectToken = Hashing.SHA256(value); }
        }
        public bool HasConnectToken {
            get { return _ConnectToken != null && _ConnectToken.Length > 0; }
        }


        public Action<ClientInformation> OnConnect;
        public Action<ClientInformation> OnDisconnect;
        public Action<ClientInformation, NetPacket> OnDataReceived;
        public Action<ClientInformation, NetPacket> OnDataSent;


        public NetamiteServer() { }

        public NetamiteServer(int maxClients) : base() {
            this.MaxClients = maxClients;
        }

        /// <summary>
        /// This function should not be called.
        /// Please use the function on a super class.
        /// </summary>
        public virtual void Start() { }

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
    }
}
