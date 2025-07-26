using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

using TaxChain.Daemon.P2P;
using Microsoft.Extensions.Logging;

namespace TaxChain.Daemon
{
    class Program
    {
        public static async void Main(string[] args)
        {
            Console.WriteLine("Daemon program runs");
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                    {
                        services.AddHostedService<Services.ControlService>();
                        services.AddSingleton<Storage.IBlockchainRepository, Storage.PSQLRepository>();
                        services.AddSingleton<P2PNetworkManager>();
                        services.AddSingleton<ILogger>();
                    })
                .Build();

            await host.RunAsync();
        }
    }
}
