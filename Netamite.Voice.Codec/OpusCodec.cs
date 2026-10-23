using System;
using Concentus.Enums;
using Concentus.Structs;
using Netamite.Voice.Codec.Opus;

namespace Netamite.Voice.Codec {
    /// <summary>
    /// Opus via Concentus, a fully managed port of libopus. Its source is compiled into this assembly (see Concentus/),
    /// so there are no native libraries or extra assemblies that a host (e.g. a mod loader) has to resolve.
    /// </summary>
    public class OpusCodec : AudioCodec {
        public string Name => "Opus";

        private int _bitrate = 64;
        private int _complexity = 5;
        private double _frameSize = 20;
        private int _packetLoss = 0;
        private bool _vbr = false;
        private bool _cvbr = false;
        private OpusApplication _application = OpusApplication.OPUS_APPLICATION_AUDIO;

        private BasicBufferShort _incomingSamples = new BasicBufferShort(48000);

        private OpusEncoder _encoder;
        private OpusDecoder _decoder;

        private byte[] scratchBuffer = new byte[10000];

        public OpusCodec() {
            _encoder = OpusEncoder.Create(48000, 1, _application);

            SetBitrate(_bitrate);
            SetComplexity(_complexity);
            SetVBRMode(_vbr, _cvbr);
            _decoder = OpusDecoder.Create(48000, 1);
        }

        public void SetBitrate(int bitrate) {
            _bitrate = bitrate;
            _encoder.Bitrate = (_bitrate * 1024);
        }

        public void SetComplexity(int complexity) {
            _complexity = complexity;
            _encoder.Complexity = (_complexity);
        }

        public void SetFrameSize(double frameSize) {
            _frameSize = frameSize;
        }

        public void SetPacketLoss(int loss) {
            _packetLoss = loss;
            if (loss > 0) {
                _encoder.PacketLossPercent = _packetLoss;
                _encoder.UseInbandFEC = true;
            } else {
                _encoder.PacketLossPercent = 0;
                _encoder.UseInbandFEC = false;
            }
        }

        public void SetApplication(OpusApplication application) {
            _application = application;
            _encoder.Application = _application;
        }

        public void SetVBRMode(bool vbr, bool constrained) {
            _vbr = vbr;
            _cvbr = constrained;
            _encoder.UseVBR = vbr;
            _encoder.UseConstrainedVBR = constrained;
        }

        private int GetFrameSize() {
            return (int)(48000 * _frameSize / 1000);
        }

        public byte[] Encode(byte[] data) {
            int frameSize = GetFrameSize();

            if (data != null) {
                short[] newData = new short[data.Length / 2];
                Buffer.BlockCopy(data, 0, newData, 0, newData.Length * 2);
                _incomingSamples.Write(newData);
            } else {
                // If input is null, assume we are at end of stream and pad the output with zeroes
                int paddingNeeded = _incomingSamples.Available() % frameSize;
                if (paddingNeeded > 0) {
                    _incomingSamples.Write(new short[paddingNeeded]);
                }
            }

            int packetSize = 0;

            if (_incomingSamples.Available() >= frameSize) {
                short[] nextFrameData = _incomingSamples.Read(frameSize);
                packetSize = _encoder.Encode(nextFrameData, 0, frameSize, scratchBuffer, 0, scratchBuffer.Length);
            }

            byte[] finalOutput = new byte[packetSize];
            Array.Copy(scratchBuffer, 0, finalOutput, 0, packetSize);
            return finalOutput;
        }

        public byte[] Decode(byte[] data) {
            int frameSize = GetFrameSize();

            short[] outputBuffer = new short[frameSize];

            bool lostPacket = new Random().Next(0, 100) < _packetLoss || data == null;
            if (!lostPacket) {
                // Normal decoding
                _decoder.Decode(data, 0, data.Length, outputBuffer, 0, frameSize, false);
            } else {
                // packet loss path, null input makes the decoder conceal the missing frame
                _decoder.Decode(null, 0, 0, outputBuffer, 0, frameSize, false);
            }

            byte[] output = new byte[outputBuffer.Length * 2];
            Buffer.BlockCopy(outputBuffer, 0, output, 0, output.Length);
            return output;
        }
    }
}
