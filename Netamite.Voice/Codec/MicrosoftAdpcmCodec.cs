using NAudio.Wave;

namespace Netamite.Voice.Codec {
    internal class MicrosoftAdpcmCodec : AcmCodec {
        public MicrosoftAdpcmCodec()
            :base(new WaveFormat(8000, 16, 1), new AdpcmWaveFormat(8000, 1)) {
        }

        public new string Name => "Microsoft ADPCM";
    }
}
