using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

#if !NETCOREAPP2_1
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets.Internal;
#endif

namespace TcpEcho
{
    public class TcpEchoServer : BackgroundService
    {
        private Socket _listenSocket;
        private volatile bool _unbinding;

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, 8087));

            Console.WriteLine("Listening on port 8087");

            _listenSocket.Listen(120);

            return base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Shutting down server...");

            if (_listenSocket == null)
            {
                return;
            }

            _unbinding = true;
            _listenSocket.Dispose();

            await base.StopAsync(cancellationToken);

            _unbinding = false;
            _listenSocket = null;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
            => RunAcceptLoopAsync(stoppingToken);

        private async Task RunAcceptLoopAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var socket = await _listenSocket.AcceptAsync();
                    _ = ProcessLinesAsync(socket, stoppingToken);
                }
                catch (SocketException) when (_unbinding)
                {
                    // Eat the exception (on unbinding only).
                }
            }

            Debug.WriteLine("Accept-loop shut down");
        }

        private async Task ProcessLinesAsync(Socket socket, CancellationToken stoppingToken)
        {
            Console.WriteLine($"[{socket.RemoteEndPoint}]: connected");

            var pipe = new Pipe();
            Task writing = FillPipeAsync(socket, pipe.Writer, stoppingToken);
            Task reading = ReadPipeAsync(socket, pipe.Reader, stoppingToken);

            await Task.WhenAll(reading, writing);

            Console.WriteLine($"[{socket.RemoteEndPoint}]: disconnected");
        }

        private async Task FillPipeAsync(Socket socket, PipeWriter writer, CancellationToken stoppingToken)
        {
            const int minimumBufferSize = 512;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Request a minimum of 512 bytes from the PipeWriter
                    Memory<byte> memory = writer.GetMemory(minimumBufferSize);
#if NETCOREAPP2_1
                    int bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None, stoppingToken);
#else
                    int bytesRead = await socket.ReceiveAsync(memory.GetArray(), SocketFlags.None);
#endif
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    // Tell the PipeWriter how much was read
                    writer.Advance(bytesRead);
                }
                catch
                {
                    break;
                }

                // Make the data available to the PipeReader
                FlushResult result = await writer.FlushAsync(stoppingToken);

                if (result.IsCompleted)
                {
                    break;
                }
            }

            if (stoppingToken.IsCancellationRequested)
            {
                Debug.WriteLine("FillPipe shut down");
            }

            // Signal to the reader that we're done writing
            writer.Complete();
        }

        private async Task ReadPipeAsync(Socket socket, PipeReader reader, CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ReadResult result = await reader.ReadAsync(stoppingToken);

                ReadOnlySequence<byte> buffer = result.Buffer;
                SequencePosition? position = null;

                do
                {
                    // Find the EOL
                    position = buffer.PositionOf((byte)'\n');

                    if (position != null)
                    {
                        var line = buffer.Slice(0, position.Value);
                        ProcessLine(socket, line);

                        // This is equivalent to position + 1
                        var next = buffer.GetPosition(1, position.Value);

                        // Skip what we've already processed including \n
                        buffer = buffer.Slice(next);
                    }
                }
                while (position != null);

                // We sliced the buffer until no more data could be processed
                // Tell the PipeReader how much we consumed and how much we left to process
                reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                {
                    break;
                }
            }

            if (stoppingToken.IsCancellationRequested)
            {
                Debug.WriteLine("ReadPipe shut down");
            }

            reader.Complete();
        }

        private void ProcessLine(Socket socket, in ReadOnlySequence<byte> buffer)
        {
            Console.Write($"[{socket.RemoteEndPoint}]: ");
            foreach (var segment in buffer)
            {
#if NETCOREAPP2_1
                string s = Encoding.UTF8.GetString(segment.Span);
#else
                ArraySegment<byte> array = segment.GetArray();
                string s = Encoding.UTF8.GetString(array.Array, array.Offset, array.Count);
#endif
                Console.Write(s);
            }
            Console.WriteLine();
        }
    }
}
