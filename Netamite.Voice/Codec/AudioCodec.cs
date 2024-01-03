using NAudio.Wave;

namespace Netamite.Voice.Codec {
    public interface AudioCodec {

        string Name { get; }
        WaveFormat RecordFormat { get; }

        byte[] Encode(byte[] data);
        byte[] Decode(byte[] data);

    }
}
