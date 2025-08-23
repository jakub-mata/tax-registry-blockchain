using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using TaxChain.Daemon.P2P;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace TaxChain.Daemon
{
    class Program
    {
        public static volatile bool VerboseMode = true;
        public static void Main(string[] args)
        {
            var root = Directory.GetCurrentDirectory();
            var dotenv = Path.Combine(root, "src/TaxChain.Daemon/.env");
            DotEnv.Load(dotenv);

            var host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddEnvironmentVariables();
                })
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
