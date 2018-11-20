using System;
using System.Diagnostics;
using System.Linq;
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
            var messageSize = args.FirstOrDefault();

            var clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);

            Console.WriteLine("Connecting to port 8087");

            clientSocket.Connect(new IPEndPoint(IPAddress.Loopback, 8087));

            if (messageSize == null)
            {
                var buffer = new byte[1];
                while (true)
                {

                    buffer[0] = (byte)Console.Read();
                    await clientSocket.SendAsync(new ArraySegment<byte>(buffer, 0, 1), SocketFlags.None);
                }
            }
            else
            {
                var count = int.Parse(messageSize);
                var buffer = Encoding.ASCII.GetBytes(new string('a', count) + Environment.NewLine);
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                for (int i = 0; i < 1_000_000; i++)
                {
                    await clientSocket.SendAsync(new ArraySegment<byte>(buffer), SocketFlags.None);
                }
                stopwatch.Stop();

                Console.WriteLine($"Elapsed {stopwatch.Elapsed.TotalSeconds:F} sec.");
            }
        }
    }
}
