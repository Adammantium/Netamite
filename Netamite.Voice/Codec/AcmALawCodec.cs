using NAudio.Wave;

namespace Netamite.Voice.Codec {
    public class AcmALawCodec : AcmCodec {
        public new string Name => "ACM G.711 a-law";

        public AcmALawCodec()
            : base(new WaveFormat(8000, 16, 1), WaveFormat.CreateALawFormat(8000, 1)) {
        }
    }
}
