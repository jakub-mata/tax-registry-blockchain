using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using DotNetEnv;
using TaxChain.Daemon.P2P;
using Microsoft.Extensions.Logging;

namespace TaxChain.Daemon
{
    class Program
    {
        public static volatile bool VerboseMode = true;
        public static void Main(string[] args)
        {
            Env.Load(".env");
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
                        services.AddSingleton<INetworkManaging, P2PNode>();
                        services.AddSingleton<Storage.IBlockchainRepository, Storage.PGSQLRepository>();
                        services.AddSingleton(() => VerboseMode);
                    })
                .Build();

            host.Run();
        }
    }
}
