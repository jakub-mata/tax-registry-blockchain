using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

using TaxChain.Daemon.P2P;
using Microsoft.Extensions.Logging;

namespace TaxChain.Daemon
{
    class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Daemon program runs");
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                    {
                        services.AddHostedService<Services.ControlService>();
                        services.AddSingleton<Storage.IBlockchainRepository, Storage.PGSQLRepository>();
                        services.AddSingleton<P2PNetworkManager>();
                    })
                .Build();

            host.Run();
        }
    }
}
