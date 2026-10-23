namespace Netamite.Voice.Codec {
    public interface AudioCodec {

        string Name { get; }

        byte[] Encode(byte[] data);
        byte[] Decode(byte[] data);

    }
}
