namespace Netamite.Network.Packet {
    public enum PacketType : byte {
        UNKNOWN                 = 0,

        ESTABLISH_CONNECTION    = 1,
        ERROR                   = 2,
        DISCONNECT              = 3,
        WELCOME                 = 4,
        PING                    = 5,
        TIME_SYNCHRONIZATION    = 6,
        MESSAGE                 = 7,
    }
}
