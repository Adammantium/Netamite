using UnityEngine;
using Netamite.Unity.Voice.Packet;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Debug = UnityEngine.Debug;

namespace Netamite.Unity.Voice {
    public class VoiceReader : MonoBehaviour {
        [Header("Microphone Settings")]
        [Tooltip("Device name to use for microphone input. Leave empty for default device.")]
        [SerializeField] private string microphoneDeviceName = null; // null = default microphone

        [Tooltip("Sample rate of the microphone recording. Must match codec requirements (16000 for G722).")]
        [SerializeField] private int sampleRate = 16000;
        
        [Tooltip("Length of audio buffer in seconds.")]
        [SerializeField] private int bufferLengthSeconds = 1;

        [Tooltip("How often to capture and process microphone data.")]
        [SerializeField] private float captureFrequency = 0.05f; // 50ms update frequency
        
        [Tooltip("How many packets should be held back to dont give latency for silence.")]
        [SerializeField] private int packet_holdback = 1;

        [Tooltip("Most chunks that may wait for the processing thread. When the game stalls, older audio gets dropped instead of piling up as delay.")]
        [SerializeField] private int maxQueuedChunks = 4;

        public delegate void onAudioPacketReady(UnityAudioPacket packet);
        public onAudioPacketReady OnAudioPacketReady;

        /// <summary>
        /// Min Volume before sending data at all
        /// Range 0.0 - 1.0 (inclusive)
        /// </summary>
        public float MinVolumePeak = 0.4f;

        public bool CalculateTotalVolumePeak = false;
        
        /// <summary>
        /// How many samples should be processed after the microphone is not reaching its peak anymore, so it wont suddenly cut off
        /// </summary>
        public int SampleSuffixCount = 5;
        
        private int additional_samples = 0;

        public static string[] AvailableMicrophoneDevices => Microphone.devices;

        public bool talking = false;

        /// <summary>
        /// Contains the current volume of the microphone input
        /// </summary>
        /// <value>The current volume.</value>
        public float currentVolume { get; set; }

        // References
        private UnityVoiceClient voiceClient;
        private AudioClip microphoneClip;
        private int chunkSize;
        private int previousPosition = 0;
        private Coroutine processingCoroutine;

        // Microphone.GetPosition and AudioClip.GetData only work on the main thread, so the coroutine just copies the
        // samples out and everything else (conversion, encoding, sending) happens on this thread
        private Thread processingThread;
        private BlockingCollection<float[]> chunkQueue;
        private readonly ConcurrentBag<float[]> freeChunks = new ConcurrentBag<float[]>();

        /// <summary>
        /// Logs once per second where the VoiceReader spends its time, to find out what causes lag
        /// </summary>
        public static bool LogPerformance = false;

        // Performance counters, in Stopwatch ticks. Written by both threads, so only touched with Interlocked
        private long perfMainTicks, perfMainMaxTicks, perfMainFrames;
        private long perfChunks, perfDropped, perfSent;
        private long perfConvertTicks, perfEncodeTicks, perfSendTicks, perfProcessMaxTicks;
        private float perfNextLog;
        private int perfGcCount;

        // State tracking
        private bool isCapturing = false;
        private bool isInitialized = false;
        private int channels = 1; // Mono
        
        /// <summary>
        /// Initialize with a VoiceClient instance
        /// </summary>
        public void Initialize(UnityVoiceClient voiceClient) {
            if (isInitialized) {
                Stop();
            }

            this.voiceClient = voiceClient;
            isInitialized = true;

            if(voiceClient != null)
                Start();
        }


        /// <summary>
        /// Start microphone capture
        /// </summary>
        public void Start() {
            if (!isInitialized) {
                Debug.LogError("[MicrophoneCapture] Cannot start capture: VoiceClient not initialized. Call Initialize() first.");
                return;
            }

            if (isCapturing) {
                return;
            }
            
            // Log available microphone devices
            Debug.Log("[MicrophoneCapture] Available microphones:");
            foreach (string device in Microphone.devices) {
                Debug.Log($"- {device}");
            }

            // Start microphone recording
            microphoneClip = Microphone.Start(microphoneDeviceName, true, bufferLengthSeconds, sampleRate);

            // Wait for microphone to initialize
            while (Microphone.GetPosition(microphoneDeviceName) <= 0) { }
            previousPosition = Microphone.GetPosition(microphoneDeviceName);

            // Prepare buffers - calculate appropriate buffer size based on capture frequency
            int samplesPerCapture = Mathf.CeilToInt(sampleRate * channels * captureFrequency);
            if (samplesPerCapture != chunkSize) {
                while (freeChunks.TryTake(out _)) { } // Pooled chunks of the old size are useless now
            }
            chunkSize = samplesPerCapture;

            // Start processing
            chunkQueue = new BlockingCollection<float[]>(new ConcurrentQueue<float[]>());
            BlockingCollection<float[]> queue = chunkQueue;
            byte[] pcmBuffer = new byte[samplesPerCapture * 2]; // 16-bit = 2 bytes per sample, one per thread
            processingThread = new Thread(() => ProcessQueue(queue, pcmBuffer)) { IsBackground = true, Name = "VoiceReader" };
            processingThread.Start();

            processingCoroutine = StartCoroutine(ProcessMicrophone());
            isCapturing = true;

            Debug.Log($"[MicrophoneCapture] Started capturing from '{(string.IsNullOrEmpty(microphoneDeviceName) ? "default device" : microphoneDeviceName)}' at {sampleRate}Hz");
        }

        /// <summary>
        /// Stop microphone capture
        /// </summary>
        public void Stop() {
            if (!isCapturing) {
                return;
            }

            if (processingCoroutine != null) {
                StopCoroutine(processingCoroutine);
                processingCoroutine = null;
            }

            // Lets the processing thread finish the queued chunks and exit
            chunkQueue?.CompleteAdding();
            chunkQueue = null;
            processingThread = null;

            Microphone.End(microphoneDeviceName);
            microphoneClip = null;
            isCapturing = false;

            Debug.Log("[MicrophoneCapture] Stopped capturing microphone");
        }
        
        public void SetDevice(string deviceName) {
            if (deviceName == microphoneDeviceName) {
                return;
            }

            bool wasCapturing = isCapturing;

            if (isCapturing) {
                Stop();
            }

            microphoneDeviceName = deviceName;

            if (wasCapturing && isInitialized) {
                Start();
            }
        }

        private int GetSamplesAvailable(int currentPosition) {
            int samplesAvailable;
            
            if (currentPosition < previousPosition) {
                // Wraparound case
                samplesAvailable = (microphoneClip.samples - previousPosition) + currentPosition;
            } else {
                samplesAvailable = currentPosition - previousPosition;
            }
            
            return samplesAvailable;
        }

        private Queue<UnityAudioPacket> buffer = new Queue<UnityAudioPacket>();
        
        private IEnumerator ProcessMicrophone() {
            while (true) {
                if (microphoneClip == null) {
                    yield return null;
                    continue;
                }

                long frameStart = Stopwatch.GetTimestamp();

                int currentPosition = Microphone.GetPosition(microphoneDeviceName);
                int samplesAvailable = GetSamplesAvailable(currentPosition);

                // The game stalled (loading, hitch): skip the old audio instead of sending it late,
                // also keeps the read position from falling a whole clip length behind and reading overwritten audio
                int maxBacklog = chunkSize * maxQueuedChunks;
                if (samplesAvailable > maxBacklog) {
                    previousPosition = (previousPosition + samplesAvailable - maxBacklog) % microphoneClip.samples;
                    samplesAvailable = maxBacklog;
                }

                // Only copy the samples here, the processing thread does the rest
                while (samplesAvailable >= chunkSize) {
                    float[] chunk = RentChunk();
                    microphoneClip.GetData(chunk, previousPosition);

                    previousPosition = (previousPosition + chunkSize) % microphoneClip.samples;
                    samplesAvailable -= chunkSize;

                    // Processing thread can't keep up, drop the oldest audio
                    while (chunkQueue.Count >= maxQueuedChunks && chunkQueue.TryTake(out float[] dropped)) {
                        freeChunks.Add(dropped);
                        Interlocked.Increment(ref perfDropped);
                    }
                    chunkQueue.Add(chunk);
                }

                if (LogPerformance) {
                    long frameTicks = Stopwatch.GetTimestamp() - frameStart;
                    perfMainTicks += frameTicks;
                    perfMainFrames++;
                    if (frameTicks > perfMainMaxTicks) perfMainMaxTicks = frameTicks;
                    LogPerformanceStats();
                }

                yield return null;
            }
        }

        private static double Ms(long ticks) {
            return ticks * 1000.0 / Stopwatch.Frequency;
        }

        // Main thread, once per second
        private void LogPerformanceStats() {
            if (Time.unscaledTime < perfNextLog) return;
            bool first = perfNextLog == 0;
            perfNextLog = Time.unscaledTime + 1f;

            int gcCount = GC.CollectionCount(0);
            int gcs = gcCount - perfGcCount;
            perfGcCount = gcCount;

            long chunks = Interlocked.Exchange(ref perfChunks, 0);
            long sent = Interlocked.Exchange(ref perfSent, 0);
            long dropped = Interlocked.Exchange(ref perfDropped, 0);
            double convert = Ms(Interlocked.Exchange(ref perfConvertTicks, 0));
            double encode = Ms(Interlocked.Exchange(ref perfEncodeTicks, 0));
            double send = Ms(Interlocked.Exchange(ref perfSendTicks, 0));
            double processMax = Ms(Interlocked.Exchange(ref perfProcessMaxTicks, 0));
            double main = Ms(perfMainTicks), mainMax = Ms(perfMainMaxTicks);
            long frames = perfMainFrames;
            perfMainTicks = perfMainMaxTicks = perfMainFrames = 0;

            if (first) return; // Counters only cover part of a second

            Debug.Log($"[VoiceReader] {frames} fps | main thread {(frames > 0 ? main / frames : 0):F2} ms/frame (max {mainMax:F2}) | " +
                      $"processing thread {chunks} chunks, {sent} sent, {dropped} dropped, per chunk: convert {(chunks > 0 ? convert / chunks : 0):F2} ms, " +
                      $"encode {(sent > 0 ? encode / sent : 0):F2} ms, send {(sent > 0 ? send / sent : 0):F2} ms (slowest chunk {processMax:F2} ms) | " +
                      $"GCs {gcs} | talking {talking}");
        }

        private float[] RentChunk() {
            return freeChunks.TryTake(out float[] chunk) ? chunk : new float[chunkSize];
        }

        private void ProcessQueue(BlockingCollection<float[]> queue, byte[] pcmBuffer) {
            buffer.Clear();

            foreach (float[] chunk in queue.GetConsumingEnumerable()) {
                long start = Stopwatch.GetTimestamp();
                try {
                    ProcessChunk(chunk, pcmBuffer);
                } catch (Exception e) {
                    Debug.LogException(e);
                }
                long ticks = Stopwatch.GetTimestamp() - start;

                Interlocked.Increment(ref perfChunks);
                long max;
                while (ticks > (max = Interlocked.Read(ref perfProcessMaxTicks)) && Interlocked.CompareExchange(ref perfProcessMaxTicks, ticks, max) != max) { }

                if (chunk.Length == chunkSize) freeChunks.Add(chunk);
            }
        }

        // Runs on the processing thread, so OnAudioPacketReady and OnTalkingStateChanged get raised there as well
        private void ProcessChunk(float[] samples, byte[] pcmBuffer) {
            long start = Stopwatch.GetTimestamp();
            ConvertFloatToPCM16(samples, pcmBuffer);

            float max = GetAudioPeak16bit(pcmBuffer, pcmBuffer.Length, MinVolumePeak);
            currentVolume = max;
            Interlocked.Add(ref perfConvertTicks, Stopwatch.GetTimestamp() - start);

            if (max > MinVolumePeak || additional_samples > 0) {
                if (max > MinVolumePeak) additional_samples = SampleSuffixCount;
                else additional_samples--;

                if (voiceClient == null) return;
                start = Stopwatch.GetTimestamp();
                UnityAudioPacket packet = new UnityAudioPacket(voiceClient.client.ClientId, voiceClient.Codec.Encode(pcmBuffer));
                Interlocked.Add(ref perfEncodeTicks, Stopwatch.GetTimestamp() - start);

                if (talking) {
                    SendPacket(packet);
                    return;
                }

                buffer.Enqueue(packet);
                if (buffer.Count > packet_holdback) {
                    while (buffer.Count > 0) {
                        SendPacket(buffer.Dequeue());
                    }
                    talking = true;
                    voiceClient.OnTalkingStateChanged?.Invoke(true);
                }
            } else {
                // Drop held back packets so they don't get sent as stale audio on the next burst
                buffer.Clear();
                if (talking && voiceClient != null) voiceClient.OnTalkingStateChanged?.Invoke(false);
                talking = false;
            }
        }

        private void SendPacket(UnityAudioPacket packet) {
            long start = Stopwatch.GetTimestamp();
            OnAudioPacketReady?.Invoke(packet);
            Interlocked.Add(ref perfSendTicks, Stopwatch.GetTimestamp() - start);
            Interlocked.Increment(ref perfSent);
        }

        private float GetAudioPeak16bit(byte[] buffer, int bytesRecorded, float threshold = 1f) {
            float max = 0;
            // interpret as 16 bit audio
            for (int index = 0; index < bytesRecorded; index += 2) {
                short sample = (short)((buffer[index + 1] << 8) |
                                        buffer[index + 0]);
                // to floating point
                var sample32 = (sample / (float) short.MaxValue) * 5;
                // absolute value
                if (sample32 < 0) sample32 = -sample32;
                // is this the max value?
                if (sample32 > max) max = sample32;
                
                if(!CalculateTotalVolumePeak && max > threshold) break;
            }
            if (max > 1) return 1;
            return max;
        }

        /// <summary>
        /// Converts Unity's float audio format [-1.0, 1.0] to 16-bit PCM format required by the voice codec
        /// </summary>
        private void ConvertFloatToPCM16(float[] samples, byte[] output) {
            // Ensure buffer sizes match our expectations
            int bytesToWrite = Math.Min(samples.Length * 2, output.Length);
            int samplesToConvert = bytesToWrite / 2;

            for (int i = 0; i < samplesToConvert; i++) {
                // Convert float [-1.0, 1.0] to short [-32768, 32767]
                short value = (short)(samples[i] * 32767f);

                // Write as little-endian (LSB first)
                output[i * 2] = (byte)(value & 0xff);
                output[i * 2 + 1] = (byte)((value >> 8) & 0xff);
            }
        }

        private void OnDisable() {
            if (isCapturing) {
                Stop();
            }
        }
    }
}
