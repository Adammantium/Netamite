using System;

namespace Netamite.Voice.Codec {
    public class AudioCodec {

        public virtual string Name => GetType().Name;

        /// <summary>
        /// Creates a new, independent instance with the same settings. Codecs keep state between packets,
        /// so every remote speaker needs their own decoder, one shared decoder garbles the audio of everyone talking at once.
        /// Override this when the codec has constructor parameters or settings that matter for decoding.
        /// </summary>
        public virtual AudioCodec CreateNew() {
            return (AudioCodec) Activator.CreateInstance(GetType());
        }

        internal byte[] encodeBuffer = new byte[0];
        internal byte[] decodeBuffer = new byte[0];
        // Separate for encoding and decoding: encoding runs on the main thread, decoding on the network receive thread
        internal short[] encodePcmBuffer = new short[0];
        internal short[] decodePcmBuffer = new short[0];
        
        internal void ensureOutputBufferSize(ref byte[] outputBuffer, int size, bool matchSize = false) {
            if (outputBuffer == null || outputBuffer.Length < size || (matchSize && size != outputBuffer.Length))
                outputBuffer = new byte[size];
            
        }
        internal void ensurePcmBufferSize(ref short[] pcmBuffer, int size, bool matchSize = false) {
            if (pcmBuffer == null || pcmBuffer.Length < size || (matchSize && size != pcmBuffer.Length))
                pcmBuffer = new short[size];
        }

        /// <summary>
        /// Encodes 16 bit PCM. Returns a new array with exactly the encoded bytes, as the packet holding it gets
        /// serialized on the send thread while the internal buffer is already reused for the next chunk
        /// </summary>
        public byte[] Encode(byte[] input) {
            if (input != null) ensureOutputBufferSize(ref encodeBuffer, input.Length * 2);
            
            int length = Encode(input, ref encodeBuffer);
            
            return Trim(encodeBuffer, length);
        }

        /// <summary>
        /// Decodes to 16 bit PCM. Returns a new array with exactly the decoded bytes
        /// </summary>
        public byte[] Decode(byte[] input) {
            if (input != null) ensureOutputBufferSize(ref decodeBuffer, input.Length * 2);
            
            int length = Decode(input, ref decodeBuffer);
            
            return Trim(decodeBuffer, length);
        }

        private static byte[] Trim(byte[] buffer, int length) {
            byte[] result = new byte[length];
            Buffer.BlockCopy(buffer, 0, result, 0, length);
            return result;
        }

        /// <summary>
        /// Encodes into outputBuffer (grow it with ensureOutputBufferSize if needed)
        /// </summary>
        /// <returns>Number of bytes written to outputBuffer</returns>
        public virtual int Encode(byte[] input, ref byte[] outputBuffer) { return 0; }

        /// <summary>
        /// Decodes into outputBuffer as 16 bit PCM (grow it with ensureOutputBufferSize if needed)
        /// </summary>
        /// <returns>Number of bytes written to outputBuffer</returns>
        public virtual int Decode(byte[] input, ref byte[] outputBuffer) { return 0; }

    }
}
