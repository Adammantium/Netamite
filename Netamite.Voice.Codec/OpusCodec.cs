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
        private OpusApplication _application = OpusApplication.OPUS_APPLICATION_VOIP;

        private readonly int _sampleRate;
        private BasicBufferShort _incomingSamples;

        private OpusEncoder _encoder;
        private OpusDecoder _decoder;

        private byte[] scratchBuffer = new byte[10000];

        /// <param name="sampleRate">Sample rate of the PCM data, has to match the recording/playback (VoiceReader records at 16000).
        /// Opus supports 8000, 12000, 16000, 24000 and 48000</param>
        public OpusCodec(int sampleRate = 16000) {
            _sampleRate = sampleRate;
            _incomingSamples = new BasicBufferShort(sampleRate); // One second of audio
            _encoder = OpusEncoder.Create(sampleRate, 1, _application);

            SetBitrate(_bitrate);
            SetComplexity(_complexity);
            SetVBRMode(_vbr, _cvbr);
            _decoder = OpusDecoder.Create(sampleRate, 1);
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
            return (int)(_sampleRate * _frameSize / 1000);
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

            // Encode every complete frame, the input chunks (e.g. 50ms from the VoiceReader) don't line up with the
            // Opus frame sizes and leftover samples would otherwise pile up. Each frame is prefixed with its length
            int outCursor = 0;

            while (_incomingSamples.Available() >= frameSize) {
                short[] nextFrameData = _incomingSamples.Read(frameSize);
                int frameBytes = _encoder.Encode(nextFrameData, 0, frameSize, scratchBuffer, outCursor + 2, scratchBuffer.Length - outCursor - 2);
                scratchBuffer[outCursor] = (byte) frameBytes;
                scratchBuffer[outCursor + 1] = (byte) (frameBytes >> 8);
                outCursor += 2 + frameBytes;
            }

            byte[] finalOutput = new byte[outCursor];
            Array.Copy(scratchBuffer, 0, finalOutput, 0, outCursor);
            return finalOutput;
        }

        public byte[] Decode(byte[] data) {
            int frameSize = GetFrameSize();

            bool lostPacket = new Random().Next(0, 100) < _packetLoss || data == null;
            if (lostPacket) {
                // packet loss path, null input makes the decoder conceal the missing frame
                short[] concealed = new short[frameSize];
                _decoder.Decode(null, 0, 0, concealed, 0, frameSize, false);
                return ToBytes(concealed, frameSize);
            }

            // Count the length prefixed frames first, to decode straight into one buffer
            int frames = 0;
            for (int cursor = 0; cursor + 2 <= data.Length; cursor += 2 + (data[cursor] | (data[cursor + 1] << 8))) frames++;

            short[] outputBuffer = new short[frames * frameSize];
            int samples = 0;
            for (int cursor = 0; cursor + 2 <= data.Length;) {
                int frameBytes = data[cursor] | (data[cursor + 1] << 8);
                if (cursor + 2 + frameBytes > data.Length) break; // Truncated packet

                samples += _decoder.Decode(data, cursor + 2, frameBytes, outputBuffer, samples, frameSize, false);
                cursor += 2 + frameBytes;
            }

            return ToBytes(outputBuffer, samples);
        }

        private static byte[] ToBytes(short[] samples, int count) {
            byte[] output = new byte[count * 2];
            Buffer.BlockCopy(samples, 0, output, 0, output.Length);
            return output;
        }
    }
}
