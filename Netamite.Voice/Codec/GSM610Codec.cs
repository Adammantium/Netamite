using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netamite.Voice.Codec {
    public class GSM610Codec : AcmCodec {
        public new string Name => "GSM 6.10";

        public GSM610Codec()
            : base(new WaveFormat(8000, 16, 1), new Gsm610WaveFormat()) {
        }

    }
}
