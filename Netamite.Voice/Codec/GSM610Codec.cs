using NAudio.Wave;

namespace Netamite.Voice.Codec {
    public class GSM610Codec : AcmCodec {
        public new string Name => "GSM 6.10";

        public GSM610Codec()
            : base(new WaveFormat(8000, 16, 1), new Gsm610WaveFormat()) {
        }

    }
}
