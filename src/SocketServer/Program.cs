using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace TcpEcho
{
    class Program
    {
        private static bool _echo;

        static async Task Main(string[] args)
        {
            _echo = args.FirstOrDefault() == "echo";

            var listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, 8087));

            Console.WriteLine("Listening on port 8087");

            listenSocket.Listen(120);

            while (true)
            {
                var socket = await listenSocket.AcceptAsync();
                _ = ProcessLinesAsync(socket);
            }
        }

        private static async Task ProcessLinesAsync(Socket socket)
        {
            Console.WriteLine($"[{socket.RemoteEndPoint}]: connected");

            using (var stream = new NetworkStream(socket))
            using (var reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    ProcessLine(socket, await reader.ReadLineAsync());
                }
            }

            Console.WriteLine($"[{socket.RemoteEndPoint}]: disconnected");
        }

        private static void ProcessLine(Socket socket, string s)
        {
            if (_echo)
            {
                Console.Write($"[{socket.RemoteEndPoint}]: ");
                Console.WriteLine(s);
            }
        }
    }
}
