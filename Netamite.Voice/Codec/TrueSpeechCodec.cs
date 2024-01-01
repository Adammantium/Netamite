using NAudio.Wave;

namespace Netamite.Voice.Codec {
    public class TrueSpeechCodec : AcmCodec {
        /// <summary>
        /// Only works in windows xp or if the TrueSpeech Codec is installed
        /// </summary>
        public TrueSpeechCodec()
            : base(new WaveFormat(8000, 16, 1), new TrueSpeechWaveFormat()) {
        }

        public new string Name => "DSP Group TrueSpeech";
    }
}
