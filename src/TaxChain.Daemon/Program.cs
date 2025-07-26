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

namespace TaxChain.Daemon.Services
{
    using TaxChain.Daemon.Storage;
    using TaxChain.core;
    using System.IO.Pipes;
    using System.IO;
    using System.Collections.Generic;
    using System.Text.Json;

    public class ControlService : IHostedService
    {
        private readonly ILogger<ControlService> _logger;
        private readonly IBlockchainRepository _blockchainRepository;
        private readonly P2PNetworkManager _networkManager;
        private readonly IHostApplicationLifetime _applicationLifetime;
        private DateTime? _startupTimestamp;
        private NamedPipeServerStream? _pipeServer;
        private CancellationTokenSource _cancellationTokenSource = new();
        private Task? _listeningTask;

        public ControlService(
            P2PNetworkManager networkManager,
            IBlockchainRepository blockchainRepository,
            IHostApplicationLifetime applicationLifetime,
            ILogger<ControlService> logger)
        {
            _networkManager = networkManager;
            _blockchainRepository = blockchainRepository;
            _applicationLifetime = applicationLifetime;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _listeningTask = Task.Run(ListenForCommands, _cancellationTokenSource.Token);
            _startupTimestamp = DateTime.Now;
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
                "list" => await HandleListCommand(),
                "remove" => await HandleRemoveCommand(request.Parameters),
                "create" => await HandleCreateCommand(request.Parameters),
                "verify" => await HandleVerifyCommand(request.Parameters),
                "fetch" => await HandleFetchCommand(request.Parameters),
                _ => new ControlResponse { Success = false, Message = $"Unknown command: {request.Command}" }
            };
        }

        private async Task<ControlResponse> HandleFetchCommand(Dictionary<string, object>? parameters)
        {
            throw new NotImplementedException();
        }

        private async Task<ControlResponse> HandleVerifyCommand(Dictionary<string, object>? parameters)
        {
            throw new NotImplementedException();
        }

        private async Task<ControlResponse> HandleCreateCommand(Dictionary<string, object>? parameters)
        {
            throw new NotImplementedException();
        }

        private async Task<ControlResponse> HandleRemoveCommand(Dictionary<string, object>? parameters)
        {
            throw new NotImplementedException();
        }

        private async Task<ControlResponse> HandleListCommand()
        {
            throw new NotImplementedException();
        }

        private Task<ControlResponse> HandleStatusCommand()
        {
            var process = Environment.ProcessId;
            var uptime = DateTime.Now - _startupTimestamp;
            
            _logger.LogInformation("Status check requested");
            
            return Task.FromResult(new ControlResponse
            {
                Success = true,
                Message = "Daemon is running",
                Data = new 
                {
                    Status = "Running", 
                    ProcessId = process,
                    Uptime = uptime,
                    Timestamp = DateTime.UtcNow
                }
            });
        }

        private async Task<ControlResponse> HandleStopCommand()
        {
            _logger.LogInformation("Stop command received");
            var response = new ControlResponse
            {
                Success = true,
                Message = "Daemon shutdown initiated",
            };

            await Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(100); // ensuring the response is sent
                    _applicationLifetime.StopApplication();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to stop the daemon.");
                }
            });
            return response;
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
