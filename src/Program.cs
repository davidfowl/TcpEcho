using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TcpEcho
{
    class Program
    {
        static async Task Main()
            => await new HostBuilder()
            .ConfigureServices(services => services.AddHostedService<TcpEchoServer>())
            .RunConsoleAsync();
    }
}
