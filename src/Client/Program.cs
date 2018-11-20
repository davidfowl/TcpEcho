using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace TcpEcho
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);

            Console.WriteLine("Connecting to port 8087");

            clientSocket.Connect(new IPEndPoint(IPAddress.Loopback, 8087));

            var buffer = new byte[1];
            using (var stream = new NetworkStream(clientSocket))
            {
                while (true)
                {
                    buffer[0] = (byte)Console.Read();
                    await stream.WriteAsync(buffer, 0 ,1);
                }
            }
        }
    }
}
