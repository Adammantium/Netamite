using Netamite.Logging;
using Netamite.Network.Packet;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace Netamite.Network.Socket {
    internal class TcpSocket : NetSocket {

        private TcpClient _client;
        public TcpClient client {
            get { return _client; }
        }

        private NetworkStream _stream;
        public NetworkStream stream {
            get { return _stream; }
        }

        public static char packet_end_indicator = (char) 4;

        /// <summary>
        /// Size of the buffer incoming data is read into. Bigger packets still work, they just need more than one read.
        /// </summary>
        public static int read_buffer_size = 16 * 1024;

        /// <summary>
        /// Packets that are waiting in the send queue are combined into one write. Once that many bytes are collected
        /// they are written out, even if the queue isn't empty yet.
        /// </summary>
        public static int send_batch_size = 16 * 1024;

        private byte[] buffer = new byte[0];

        public override bool IsConnected {
            get {
                try {
                    if(client != null && client.Client != null && client.Client.Connected && stream.CanRead) {
                        /* pear to the documentation on Poll:
                         * When passing SelectMode.SelectRead as a parameter to the Poll method it will return
                         * -either- true if Socket.Listen(Int32) has been called and a connection is pending;
                         * -or- true if data is available for reading;
                         * -or- true if the connection has been closed, reset, or terminated;
                         * otherwise, returns false
                         */

                        // Detect if client disconnected
                        if(client.Client.Poll(0, SelectMode.SelectRead)) {
                            byte[] buff = new byte[1];
                            if(client.Client.Receive(buff, SocketFlags.Peek) == 0) {
                                // Client disconnected
                                return false;
                            } else {
                                return true;
                            }
                        }

                        return true;
                    } else {
                        return false;
                    }
                } catch {
                    return false;
                }
            }
        }

        public override string TYPE => "TCP";

        public TcpSocket(TcpClient client) {
            _client = client;

            Configure(client);
            _stream = client.GetStream();

            buffer = new byte[read_buffer_size];

            StartProcessing();
        }

        public TcpSocket(string ip, int port) {
            _client = new TcpClient();
            Configure(client);

            buffer = new byte[read_buffer_size];

            IAsyncResult result = client.BeginConnect(ip, port, ConnectRequestCallback, client);
            // Begin timeout wait
            bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(10));
            // If timed out
            if(!success) {
                // End connection
                try {
                    client.EndConnect(result);
                }catch(Exception e) {
                    Log.Err(e);
                }
                Log.Err("Could not connect to server, timed out");
                return;
            }
        }

        private static void Configure(TcpClient client) {
            // Game packets are small and time critical. With Nagle's algorithm enabled the OS holds small packets back
            // until the previous ones got acknowledged, which costs up to 200ms when it meets delayed ACKs on the other side.
            // Combining small packets is done by the send queue instead (see Send / Flush), so nothing is lost by turning it off.
            client.NoDelay = true;

            // The OS defaults for the socket buffers stay untouched. Shrinking them (they used to be set to 1024 bytes)
            // limits the TCP window to a single small packet per round trip.
        }

        private void ConnectRequestCallback(IAsyncResult _result) {
            // Runs on the thread pool, an uncaught exception here takes down the whole process.
            // A failed connect leaves client.Connected false, which IPClient reports via OnConnectionError
            try {
                client.EndConnect(_result);
            }catch(SocketException e) {
                // IL2CPP reports the raw errno instead of the mapped SocketError (111 = ECONNREFUSED on Linux/Android)
                bool refused = e.SocketErrorCode == SocketError.ConnectionRefused || (int) e.SocketErrorCode == 111;
                Log.Err(refused ? "Could not connect to server: connection refused, no server is listening on that address/port"
                                : $"Could not connect to server: {e.SocketErrorCode}");
                return;
            }catch(ObjectDisposedException) {
                return;
            }
            // If the socket is already connected then stop
            if(!client.Connected) {
                return;
            }
            _stream = client.GetStream();

            StartProcessing();
        }

        public override void AwaitData() {
            try {
                stream.BeginRead(buffer, 0, buffer.Length, ReceiveCallback, null);
            }catch(SocketException e) {
                Disconnect();
                Log.Err($"Error receiving TCP data: {e}");
            }
        }

        private void ReceiveCallback(IAsyncResult _result) {
            try {
                NetworkStream current = stream;
                if(current != null && _result != null) {
                    int bytesRead = current.EndRead(_result);

                    if(bytesRead <= 0) {
                        //Disconnect();
                        return;
                    }

                    // Packets are read straight out of the receive buffer, only incomplete ones get copied
                    HandleData(buffer, 0, bytesRead);
                }
            } catch(SocketException e) {
                Disconnect();
                Log.Err($"Error receiving TCP data: {e}");
            } catch(ObjectDisposedException) { return; } catch(IOException) { return; }

            try {
                NetworkStream current = stream;
                if(current != null)
                    current.BeginRead(buffer, 0, buffer.Length, ReceiveCallback, null);
            } catch(ObjectDisposedException) { } catch(IOException) { } // Connection got closed while the packets were handled
        }

        #region Sending
        // Packets that are about to be sent as a single write, only used by the send thread
        private BinaryNetStream batch;

        internal override void Send(Outgoing outgoing) {
            TcpClient current = client;
            if(current == null || stream == null || !current.Connected) return;

            if(batch == null) batch = new BinaryNetStream();

            int batched = batch.Length();
            try {
                if(outgoing.Prepared != null) {
                    byte[] data = outgoing.Prepared.GetData();
                    batch.WriteBuffer(data, 0, data.Length);
                } else if(outgoing.Packet != null) {
                    outgoing.Packet.WriteTo(batch, true);
                }
            } catch(Exception e) {
                batch.SetLength(batched); // Don't send half a packet, everything after it would be unreadable for the receiver
                Log.Err($"Error sending data to player via TCP: {e}");
                return;
            }

            if(batch.Length() >= send_batch_size) Flush();
        }

        internal override void Flush() {
            if(batch == null) return;

            int length = batch.Length();
            if(length == 0) return;

            try {
                NetworkStream current = stream;
                if(current != null) {
                    bytesSent += length;

                    current.Write(batch.RawBuffer, batch.RawOffset, length);
                }
            } catch(SocketException) {

            } catch(Exception e) {
                Log.Err($"Error sending data to player via TCP: {e}");
            } finally {
                // A burst can grow the batch a lot, no need to hold on to all that memory for the rest of the connection
                if(batch.Capacity > send_batch_size * 8) batch = null;
                else batch.Clear();
            }
        }

        /// <summary>
        /// Writes a single packet immediately. Queued packets (QueuePacket) are combined instead, which is the faster way.
        /// </summary>
        public override void SendPacket(NetPacket packet) {
            try {
                if(client != null && stream != null && client.Connected) {
                    byte[] data = packet.GetData(true);

                    bytesSent += data.Length;

                    stream.Write(data, 0, data.Length);
                }
            } catch(SocketException) {

            } catch(Exception e) {
                Log.Err($"Error sending data to player via TCP: {e}");
            }
        }

        public override void SendPacket(PreparedPacket packet) {
            try {
                if(client != null && stream != null && client.Connected) {
                    byte[] data = packet.GetData();

                    bytesSent += data.Length;

                    stream.Write(data, 0, data.Length);
                }
            } catch(SocketException) {

            } catch(Exception e) {
                Log.Err($"Error sending data to player via TCP: {e}");
            }
        }
        #endregion

        public override void Disconnect() {
            if(!closing) {
                base.Disconnect();

                if(client != null && stream != null) {
                    stream.Flush();
                    Thread.Sleep(100);
                }

                client.Close();
                if(client != null) client.Dispose();
                if(stream != null) stream.Dispose();
                _stream = null;
                _client = null;

                onPacket = null;
            }
        }
    }
}
