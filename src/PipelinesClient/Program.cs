using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TcpEcho {
    class Program {
        private const int minimumBufferSize = 1024;

        static async Task Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("Provide an endpoint, a port and something to send. E.g.: "
                    + "client.exe some.endpoint.com 99999 something_to_send");
                return;
            }

            using (Socket clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp))
            {
                await clientSocket.ConnectAsync(args[0], int.Parse(args[1]));

                byte[] bytesToSend = UTF8Encoding.UTF8.GetBytes(args[2] + Environment.NewLine);
                clientSocket.Send(bytesToSend);

                var pipe = new Pipe();
                var writing = WriteToPipeAsync(clientSocket, pipe.Writer);
                var reading = ReadFromPipeAsync(pipe.Reader);

                await Task.WhenAll(reading, writing);

                Console.WriteLine(await reading);
            }
        }

        static async Task WriteToPipeAsync(Socket clientSocket, PipeWriter writer)
        {
            int read;
            FlushResult result;

            do
            {
                Memory<byte> memory = writer.GetMemory(minimumBufferSize);

                if((read = await clientSocket.ReceiveAsync(memory, SocketFlags.None)) == 0)
                {
                    break;
                }

                writer.Advance(read);

                if((result = await writer.FlushAsync()).IsCompleted)
                {
                    break;
                }
            }
            while(clientSocket.Available > 0);

            writer.Complete();
        }

        static async Task<string> ReadFromPipeAsync(PipeReader reader)
        {
            var strResult = "";

            while(true)
            {
                ReadResult result = await reader.ReadAsync();
                ReadOnlySequence<byte> buffer = result.Buffer;

                var content = UTF8String(buffer);
                strResult += content;
                reader.AdvanceTo(buffer.End, buffer.End);

                if(result.IsCompleted)
                {
                    break;
                }
            }

            return strResult;
        }

        private static string UTF8String(ReadOnlySequence<byte> buffer)
        {
            var result = "";
            foreach(ReadOnlyMemory<byte> segment in buffer)
            {
#if NETCOREAPP2_1
                result += UTF8Encoding.UTF8.GetString(segment.Span);
#else
                result += UTF8Encoding.UTF8.GetString(segment);
#endif
            }
            return result;
        }
    }
}

#if NET461

internal static class Extensions
{
    public static Task<int> ReceiveAsync(this Socket clientSocket, Memory<byte> memory, SocketFlags socketFlags)
    {
        var arraySegment = GetArray(memory);
        return SocketTaskExtensions.ReceiveAsync(clientSocket, arraySegment, socketFlags);
    }

    public static string GetString(this Encoding encoding, ReadOnlyMemory<byte> memory)
    {
        var arraySegment = GetArray(memory);
        return encoding.GetString(arraySegment.Array, arraySegment.Offset, arraySegment.Count);
    }

    private static ArraySegment<byte> GetArray(Memory<byte> memory)
    {
        return GetArray((ReadOnlyMemory<byte>)memory);
    }

    private static ArraySegment<byte> GetArray(ReadOnlyMemory<byte> memory)
    {
        if(!MemoryMarshal.TryGetArray(memory, out var result))
        {
            throw new InvalidOperationException("Buffer backed by array was expected");
        }

        return result;
    }
}

#endif 