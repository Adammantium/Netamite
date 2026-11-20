using Netamite.Logging;
using Netamite.Network.Packet;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Netamite.Network.Socket {
    public class NetSocket {

        public virtual string TYPE => "NetSocket";

        public virtual bool IsConnected => false;

        public Action<NetPacket> onPacket;
        public Action onDisconnect;

        public bool closing = false;

        public long bytesSent = 0;
        public long bytesReceived = 0;
        public long BytesSent {
            get {
                long val = bytesSent;
                bytesSent = 0;
                return val;
            }
        }
        public long BytesReceive {
            get {
                long val = bytesReceived;
                bytesReceived = 0;
                return val;
            }
        }

        #region Receiving
        // Start of a packet that hasn't completely arrived yet. Complete packets never end up in here,
        // they get read straight out of the buffer the data arrived in.
        private byte[] pending;
        private int pendingStart = 0;
        private int pendingEnd = 0;

        public void HandleData(byte[] data) {
            HandleData(data, 0, data.Length);
        }

        /// <summary>
        /// Feeds data of a stream (TCP) into the socket, packets can be split up in any way between calls.
        /// The array can be reused once the call returns.
        /// </summary>
        public void HandleData(byte[] data, int offset, int count) {
            if(count <= 0) return;
            bool invalid;

            if(pendingEnd == pendingStart) {
                int used = ReadPackets(data, offset, count, out invalid);
                if(invalid) return;

                // Whatever is left is the start of a packet that continues with the next call
                if(used < count) Keep(data, offset + used, count - used);
            } else {
                Keep(data, offset, count);

                int used = ReadPackets(pending, pendingStart, pendingEnd - pendingStart, out invalid);
                pendingStart += used;
                if(invalid || pendingStart == pendingEnd) pendingStart = pendingEnd = 0;
            }
        }

        /// <summary>
        /// Handles a datagram (UDP): the packets in it have to be complete, anything else in it is dropped
        /// instead of being mixed into the datagram that arrives next.
        /// </summary>
        protected void HandleDatagram(byte[] data, int offset, int count) {
            ReadPackets(data, offset, count, out _);
        }

        /// <summary>
        /// Reads all complete packets (ushort length, byte type, members) and passes them on
        /// </summary>
        /// <param name="invalid">The data can't be a packet, the stream is broken and everything buffered has to go</param>
        /// <returns>How many bytes have been processed</returns>
        private int ReadPackets(byte[] buffer, int offset, int count, out bool invalid) {
            invalid = false;
            int pos = offset;
            int end = offset + count;

            while(end - pos >= 3) { // At least 3 bytes are needed 2 bytes for length, and 1 byte for the packet type
                int length = buffer[pos] | (buffer[pos + 1] << 8);

                if(length == 0 || buffer[pos + 2] == 0) {
                    invalid = true;
                    break;
                }

                if(end - pos < length + 2) break; // Rest of the packet is still on its way

                NetPacket packet = NetPacket.ReadPacket(buffer, pos + 2, length);
                if(packet != null) {
                    HandleData(packet);
                }

                bytesReceived += length + 2;
                pos += length + 2;
            }
            return pos - offset;
        }

        private void Keep(byte[] data, int offset, int count) {
            int kept = pendingEnd - pendingStart;

            if(pending == null || pendingEnd + count > pending.Length) {
                if(pending != null && kept + count <= pending.Length) {
                    // Enough room once the already processed bytes at the front are gone
                    Buffer.BlockCopy(pending, pendingStart, pending, 0, kept);
                } else {
                    byte[] grown = new byte[Math.Max(1024, Math.Max(kept + count, pending == null ? 0 : pending.Length * 2))];
                    if(kept > 0) Buffer.BlockCopy(pending, pendingStart, grown, 0, kept);
                    pending = grown;
                }
                pendingStart = 0;
                pendingEnd = kept;
            }

            Buffer.BlockCopy(data, offset, pending, pendingEnd, count);
            pendingEnd += count;
        }

        public void HandleData(NetPacket packet) {
            if(packet == null) return;
            Action<NetPacket> handler = onPacket;
            if(handler == null) return;
            try {
                handler.Invoke(packet);
            } catch(Exception e) {
                Log.Err(e);
            }
        }
        #endregion

        #region Sending
        /// <summary>
        /// Entry of the send queue: either a packet, or a packet that is shared with other sockets and only gets serialized once
        /// </summary>
        internal struct Outgoing {
            public NetPacket Packet;
            public PreparedPacket Prepared;
        }

        internal BlockingCollection<Outgoing> processPacketQueue = new BlockingCollection<Outgoing>();
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        public void QueuePacket(NetPacket packet) {
            if(packet == null) return;
            Queue(new Outgoing { Packet = packet });
        }

        /// <summary>
        /// Queues a packet that goes out on multiple sockets, see PreparedPacket
        /// </summary>
        public void QueuePacket(PreparedPacket packet) {
            if(packet == null) return;
            Queue(new Outgoing { Prepared = packet });
        }

        private void Queue(Outgoing outgoing) {
            if(closing) return;

            try {
                processPacketQueue.Add(outgoing);
            } catch(ObjectDisposedException) {
                // Socket got closed in this very moment
            } catch(InvalidOperationException) { }
        }

        public void ProcessSendQueue() {
            ProcessSendQueue(true);
        }

        public void ProcessSendQueue(bool loop = true) {
            Outgoing outgoing;
            while(loop || processPacketQueue.Count > 0) {
                try {
                    bool has = processPacketQueue.TryTake(out outgoing, -1, cancellationTokenSource.Token); // This will wait non blocking for an item to arrive at the queue
                    if(cancellationTokenSource.IsCancellationRequested) return;
                    if(!has) continue;

                    // Send everything that piled up in one go. Sockets that can combine packets (TCP)
                    // collect them in Send and only put them on the wire in Flush.
                    int sent = 0;
                    do {
                        Send(outgoing);
                    } while(++sent < MAX_PACKETS_PER_FLUSH && processPacketQueue.TryTake(out outgoing));
                    Flush();
                }catch(ThreadAbortException) {
                    return;
                }catch(OperationCanceledException) {
                    return;
                }catch(ArgumentNullException) {
                    continue;
                } catch(ObjectDisposedException) {
                    if(closing) return;
                    continue;
                } catch(Exception e) {
                    // One broken packet must not end the thread, nothing would get sent anymore
                    Log.Err(e);
                }
            }
        }
        private const int MAX_PACKETS_PER_FLUSH = 256;

        /// <summary>
        /// Sends one entry of the queue, or collects it until Flush is called
        /// </summary>
        internal virtual void Send(Outgoing outgoing) {
            if(outgoing.Prepared != null) {
                SendPacket(outgoing.Prepared);
            } else if(outgoing.Packet != null) {
                SendPacket(outgoing.Packet);
            }
        }

        /// <summary>
        /// Puts everything on the wire that Send has collected
        /// </summary>
        internal virtual void Flush() { }

        public virtual void SendPacket(NetPacket packet) { }

        /// <summary>
        /// Sends a packet that is shared with other sockets. Override it to send packet.GetData(),
        /// by default the packet is serialized again like any other packet.
        /// </summary>
        public virtual void SendPacket(PreparedPacket packet) {
            SendPacket(packet.Packet);
        }
        #endregion

        public virtual void Disconnect() {
            if(!closing) {
                closing = true;
                if(onDisconnect != null) onDisconnect.Invoke();
                StopProcessing();
                cancellationTokenSource.Cancel();
                processPacketQueue.Dispose();
            }
        }


        private Thread processDataThread = null;
        public void StartSending() {
            if(processDataThread != null && processDataThread.IsAlive) return;
            processDataThread = new Thread(ProcessSendQueue) { IsBackground = true };
            processDataThread.Name = "NetSocket Data Send Thread";
            processDataThread.Start();
        }

        public void StopSending() {
            if(processDataThread == null) return;
            processDataThread.Abort();
        }

        protected Thread awaitDataThread = null;
        public void StartReceiving() {
            /*
            if(awaitDataThread != null && awaitDataThread.IsAlive) return;
            awaitDataThread = new Thread(AwaitData);
            awaitDataThread.Name = "NetSocket Data Read Thread";
            awaitDataThread.Start();
            */
            AwaitData();
        }

        public void StopReceiving() {
            if(awaitDataThread == null) return;
            awaitDataThread.Abort();
        }

        public void StartProcessing() {
            StartReceiving();
            StartSending();
        }

        internal void StopProcessing(bool flush = true) {
            if(flush) {
                int tries = 100;
                while(processPacketQueue.Count > 0 && tries > 100) {
                    Thread.Sleep(10);
                    tries--;
                }
                Thread.Sleep(100);
            }

            StopReceiving();
            StopSending();
        }

        public virtual void AwaitData() { }
    }
}
