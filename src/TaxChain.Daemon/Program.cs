using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System.Threading;

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
                        services.AddHostedService<Services.BlockchainDaemonService>();
                        services.AddSingleton<Storage.IBlockchainRepository, Storage.PSQLRepository>();
                        services.AddSingleton<P2PNetworkManager>();
                        services.AddSingleton<ILogger>();
                    })
                .Build();

            await host.RunAsync();
        }
    }
}

namespace TaxChain.Daemon.Services
{
    using TaxChain.Daemon.Storage;
    using TaxChain.core;
    using System.IO.Pipes;
    using System.IO;
    using System.Collections.Generic;
    using System.Text.Json;
    public class BlockchainDaemonService : IHostedService
    {
        private readonly P2PNetworkManager _networkManager;
        private readonly IBlockchainRepository _blockchainRepository;
        private readonly ILogger<BlockchainDaemonService> _logger;

        public IBlockchainRepository BlockchainRepository
        {
            get
            {
                return _blockchainRepository;
            }
        }

        public BlockchainDaemonService(
            P2PNetworkManager networkManager,
            IBlockchainRepository blockchainRepository,
            ILogger<BlockchainDaemonService> logger)
        {
            _networkManager = networkManager;
            _blockchainRepository = blockchainRepository;
            _logger = logger;
        }
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting up the blockchain daemon...");
            throw new NotImplementedException();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping the blockchain daemon...");
            throw new NotImplementedException();
        }

        public Task SynchronizeAll()
        {
            throw new NotImplementedException();
        }

        public Task SynchronizeOne(int chainId)
        {
            throw new NotImplementedException();
        }
    }

    public class ControlService : IHostedService
    {
        private readonly ILogger<ControlService> _logger;
        private NamedPipeServerStream? _pipeServer;
        private CancellationTokenSource _cancellationTokenSource = new();
        private Task? _listeningTask;

        public ControlService(ILogger<ControlService> logger)
        {
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _listeningTask = Task.Run(ListenForCommands, _cancellationTokenSource.Token);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource.Cancel();
            _pipeServer?.Close();
            if (_listeningTask != null)
                await _listeningTask;
        }

        private async Task ListenForCommands()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    _pipeServer = new NamedPipeServerStream("TaxChainControlPipe", PipeDirection.InOut);
                    await _pipeServer.WaitForConnectionAsync(_cancellationTokenSource.Token);

                    await HandleClientConnection(_pipeServer);
                    _pipeServer.Disconnect();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in control pipe listener");
                }
            }
        }

        private async Task HandleClientConnection(NamedPipeServerStream pipe)
        {
            using var reader = new StreamReader(pipe);
            using var writer = new StreamWriter(pipe) { AutoFlush = true };

            string? command;
            while ((command = await reader.ReadLineAsync()) != null)
            {
                try
                {
                    var request = JsonSerializer.Deserialize<ControlRequest>(command);
                    var response = await ProcessCommand(request);
                    var responseJson = JsonSerializer.Serialize(response);
                    await writer.WriteLineAsync(responseJson);
                }
                catch (Exception ex)
                {
                    var errorResponse = new ControlResponse
                    {
                        Success = false,
                        Message = ex.Message
                    };
                    var responseJson = JsonSerializer.Serialize(errorResponse);
                    await writer.WriteLineAsync(responseJson);
                }
            }
        }

        private async Task<ControlResponse> ProcessCommand(ControlRequest? request)
        {
            if (request == null)
                return new ControlResponse { Success = false, Message = "Invalid request" };

            return request.Command.ToLower() switch
            {
                "status" => await HandleStatusCommand(),
                "stop" => await HandleStopCommand(),
                "sync" => await HandleSyncCommand(request.Parameters),
                _ => new ControlResponse { Success = false, Message = $"Unknown command: {request.Command}" }
            };
        }

        private Task<ControlResponse> HandleStatusCommand()
        {
            return Task.FromResult(new ControlResponse
            {
                Success = true,
                Message = "Daemon is running",
                Data = new { Status = "Running", Uptime = DateTime.UtcNow }
            });
        }

        private async Task<ControlResponse> HandleStopCommand()
        {
            // Signal the application to stop
            // This would typically involve communicating with the host
            return new ControlResponse
            {
                Success = true,
                Message = "Daemon stopping"
            };
        }

        private Task<ControlResponse> HandleSyncCommand(Dictionary<string, object>? parameters)
        {
            // Implement sync logic
            return Task.FromResult(new ControlResponse
            {
                Success = true,
                Message = "Sync initiated"
            });
        }
    }
}
