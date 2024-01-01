using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netamite.Voice.Codec {
    public interface AudioCodec {

        string Name { get; }
        WaveFormat RecordFormat { get; }

        byte[] Encode(byte[] data);
        byte[] Decode(byte[] data);

    }
}
