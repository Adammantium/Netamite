using Netamite.Network.Packet;

namespace Netamite.Client.Definition {
    public class NetamiteClient {

        public virtual void SendReliable(NetPacket packet) { }
        public virtual void SendUnreliable(NetPacket packet) { }

    }
}
