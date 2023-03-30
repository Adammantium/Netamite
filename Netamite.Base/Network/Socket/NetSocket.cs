using Netamite.Logging;
using Netamite.Network.Packet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Netamite.Network.Socket {
    public class NetSocket {

        public virtual string TYPE => "NetSocket";

        public virtual bool IsConnected => false;

        public Action<NetPacket> onPacket;
        public Action onDisconnect;

        internal int _BytesSent = 0;
        public int BytesSent {
            set { _BytesSent = value; }
            get { 
                int val = _BytesSent;
                _BytesSent = 0;
                return val;
            }
        }
        internal int _BytesReceived = 0;
        public int BytesReceive {
            set { _BytesSent = value; }
            get {
                int val = _BytesSent;
                _BytesSent = 0;
                return val;
            }
        }

        private List<byte> packet_buffer = new List<byte>();

        public bool closing = false;

        public void HandleData(byte[] data) {
            packet_buffer.AddRange(data);

            #if PERFORMANCE_WARNING
            PerformanceError pe = new PerformanceError( errorPrefix: Defines.AMP
                                                      , errorMessage: "Processing of Packets is taking longer than expected, probably a performance issue. " + TYPE + " processing loop is running for {0}ms."
                                                      );
            #endif

            while(packet_buffer.Count > 3) { // At least 3 bytes are needed 2 bytes for length, and 1 byte for the packet type
                short length = BitConverter.ToInt16(new byte[] { packet_buffer[0], packet_buffer[1] }, 0);

                if(length <= 0) {
                    packet_buffer.Clear();
                    break;
                }

                if(packet_buffer.Count >= length + 2) {
                    byte[] packet_data = packet_buffer.GetRange(2, length).ToArray();

                    using(NetPacket packet = NetPacket.ReadPacket(packet_data)) {
                        if(packet == null) break;
                        HandleData(packet);
                    }

                    if(packet_buffer.Count >= length + 2) packet_buffer.RemoveRange(0, length + 2);
                } else {
                    break;
                }

                #if PERFORMANCE_WARNING
                pe.HasPerformanceIssue();
                #endif
            }
        }

        public void HandleData(NetPacket packet) {
            if(packet == null) return;
            if(onPacket == null) return;
            try {
                onPacket.Invoke(packet);
            } catch(Exception e) {
                Log.Err(e);
            }
            _BytesReceived += packet.GetData().Length;
        }

        internal BlockingCollection<NetPacket> processPacketQueue = new BlockingCollection<NetPacket>(); 
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        public void QueuePacket(NetPacket packet) {
            if(packet == null) return;
            if(closing) return;

            processPacketQueue.Add(packet);
        }

        public void ProcessSendQueue() {
            ProcessSendQueue(true);
        }

        public void ProcessSendQueue(bool loop = true) {
            NetPacket packet;
            while(loop || processPacketQueue.Count > 0) {
                try {
                    bool has = processPacketQueue.TryTake(out packet, -1, cancellationTokenSource.Token); // This will wait non blocking for an item to arrive at the queue
                    if(cancellationTokenSource.IsCancellationRequested) return;
                    if(!has) continue;

                    _BytesSent += packet.GetData().Length;

                    if(packet == null) continue;
                    SendPacket(packet);
                }catch(ThreadAbortException) { 
                    return;
                }catch(ArgumentNullException) {
                    continue;
                }
            }
        }

        public virtual void SendPacket(NetPacket packet) { }

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
            processDataThread = new Thread(ProcessSendQueue);
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

        internal virtual void AwaitData() { }
    }
}
