using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

using TaxChain.Daemon.P2P;
using Microsoft.Extensions.Logging;

namespace TaxChain.Daemon
{
    class Program
    {
        public static volatile bool VerboseMode = true;
        public static void Main(string[] args)
        {
            Console.WriteLine("Daemon program runs");
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.AddFilter((category, level) =>
                    {
                        return level >= (VerboseMode ? LogLevel.Information : LogLevel.Warning);
                    });
                })
                .ConfigureServices((hostContext, services) =>
                    {
                        services.AddHostedService<Services.ControlService>();
                        services.AddSingleton<Storage.IBlockchainRepository, Storage.PGSQLRepository>();
                        services.AddSingleton<P2PNetworkManager>();
                        services.AddSingleton(() => VerboseMode);
                    })
                .Build();

            host.Run();
        }
    }
}
