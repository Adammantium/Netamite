using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netamite.Voice.Codec {
    internal class AcmMuLawCodec : AcmCodec {
        public new string Name => "ACM G.711 mu-law";

        public AcmMuLawCodec()
            : base(new WaveFormat(8000, 16, 1), WaveFormat.CreateMuLawFormat(8000, 1)) {
        }
    }
}
