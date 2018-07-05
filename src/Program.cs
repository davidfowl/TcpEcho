using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TcpEcho
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, 8087));

            Console.WriteLine("Listening on port 8087");

            listenSocket.Listen(120);

            while (true)
            {
                var socket = await listenSocket.AcceptAsync();
                _ = AcceptAsync(socket);
            }
        }

        private static async Task AcceptAsync(Socket socket)
        {
            Console.WriteLine($"[{socket.RemoteEndPoint}]: connected");

            var pipe = new Pipe();
            Task writing = ReadFromSocketAsync(pipe.Writer);
            Task reading = ReadFromPipeAsync(pipe.Reader);

            async Task ReadFromSocketAsync(PipeWriter writer)
            {
                const int minimumBufferSize = 512;

                while (true)
                {
                    Memory<byte> memory = writer.GetMemory(minimumBufferSize);
                    int read = await socket.ReceiveAsync(memory, SocketFlags.None);
                    if (read == 0)
                    {
                        break;
                    }
                    writer.Advance(read);
                    FlushResult result = await writer.FlushAsync();

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
                writer.Complete();
            }

            async Task ReadFromPipeAsync(PipeReader reader)
            {
                while (true)
                {
                    ReadResult result = await reader.ReadAsync();

                    ReadOnlySequence<byte> buffer = result.Buffer;
                    SequencePosition? position = buffer.PositionOf((byte)'\n');

                    if (position != null)
                    {
                        ProcessLine(socket, buffer.Slice(0, position.Value));
                        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
                    }

                    reader.AdvanceTo(buffer.Start);

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }

                reader.Complete();
            }

            await reading;
            await writing;

            Console.WriteLine($"[{socket.RemoteEndPoint}]: disconnected");
        }

        private static void ProcessLine(Socket socket, in ReadOnlySequence<byte> buffer)
        {
            Console.Write($"[{socket.RemoteEndPoint}]: ");
            foreach (var segment in buffer)
            {
                Console.Write(Encoding.UTF8.GetString(segment.Span));
            }
            Console.WriteLine();
        }
    }
}
