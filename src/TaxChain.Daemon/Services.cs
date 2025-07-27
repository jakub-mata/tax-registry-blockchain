using TaxChain.Daemon.Storage;
using TaxChain.core;
using TaxChain.Daemon.P2P;
using System.IO.Pipes;
using System.IO;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace TaxChain.Daemon.Services
{
    public class ControlService : IHostedService
    {
        private readonly IBlockchainRepository _blockchainRepository;
        private readonly P2PNetworkManager _networkManager;
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly ILogger<ControlService> _logger;
        private DateTime? _startupTimestamp;
        private CancellationTokenSource _cancellationTokenSource = new();
        private Thread? _listeningThread;

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
            _startupTimestamp = DateTime.Now;
            _listeningThread = new Thread(ListenForCommands);
            _listeningThread.IsBackground = false; // Keep thread alive
            _listeningThread.Start();
            _logger.LogInformation($"Control service started on pid {Environment.ProcessId}");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource.Cancel();

            // Wait for thread to finish (with timeout)
            if (_listeningThread != null && _listeningThread.IsAlive)
            {
                try
                {
                    _listeningThread.Join(2000); // Wait up to 2 seconds
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error waiting for listening thread to stop");
                }
            }

            return Task.CompletedTask;
        }

        private void ListenForCommands()
        {
            _logger.LogInformation("Control service listening for commands...");

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                NamedPipeServerStream? pipeServer = null;
                try
                {
                    pipeServer = new NamedPipeServerStream("TaxChainControlPipe", PipeDirection.InOut);
                    _logger.LogInformation("Waiting for client connection...");

                    var waitTask = pipeServer.WaitForConnectionAsync(_cancellationTokenSource.Token);
                    waitTask.Wait(_cancellationTokenSource.Token);

                    _logger.LogInformation("Client connected");

                    HandleClientConnection(pipeServer);
                    _logger.LogInformation("Client connection handled");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    // Expected when shutting down
                    break;
                }
                catch (AggregateException ae) when (ae.InnerException is OperationCanceledException)
                {
                    _logger.LogInformation("Control service stopping (canceled)");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in control pipe listener");

                    // Brief delay to prevent tight loop on repeated errors
                    if (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        try
                        {
                            Thread.Sleep(1000);
                        }
                        catch
                        {
                            break;
                        }
                    }
                }
                finally
                {
                    if (pipeServer != null)
                    {
                        try
                        {
                            if (pipeServer.IsConnected)
                                pipeServer.Disconnect();
                            pipeServer.Dispose();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error disposing pipe server");
                        }
                    }
                }
            }
        }

        private void HandleClientConnection(NamedPipeServerStream pipe)
        {
            try
            {
                using var reader = new StreamReader(pipe);
                using var writer = new StreamWriter(pipe) { AutoFlush = true };

                string? commandLine;
                while ((commandLine = reader.ReadLine()) != null &&
                       !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        _logger.LogInformation("Received command");
                        var request = JsonSerializer.Deserialize<ControlRequest>(commandLine);
                        var response = ProcessCommand(request);
                        var responseJson = JsonSerializer.Serialize(response);
                        writer.WriteLine(responseJson);
                        _logger.LogInformation("Command processed successfully");
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "Invalid JSON received");
                        var errorResponse = new ControlResponse
                        {
                            Success = false,
                            Message = "Invalid JSON format"
                        };
                        var responseJson = JsonSerializer.Serialize(errorResponse);
                        writer.WriteLine(responseJson);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing command");
                        var errorResponse = new ControlResponse
                        {
                            Success = false,
                            Message = ex.Message
                        };
                        var responseJson = JsonSerializer.Serialize(errorResponse);
                        writer.WriteLine(responseJson);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling client connection");
            }
        }

        private ControlResponse ProcessCommand(ControlRequest? request)
        {
            if (request == null)
                return new ControlResponse { Success = false, Message = "Invalid request" };

            _logger.LogInformation($"Processing command: {request.Command}");

            return request.Command.ToLower() switch
            {
                "status" => HandleStatusCommand(),
                "stop" => HandleStopCommand(),
                "sync" => HandleSyncCommand(request.Parameters),
                "list" => HandleListCommand(),
                "remove" => HandleRemoveCommand(request.Parameters),
                "create" => HandleCreateCommand(request.Parameters),
                "verify" => HandleVerifyCommand(request.Parameters),
                "fetch" => HandleFetchCommand(request.Parameters),
                "add" => HandleAddCommand(request.Parameters),
                "pop" => HandlePopCommand(request.Parameters),
                "ledger" => HandleLedgerCommand(request.Parameters),
                _ => new ControlResponse { Success = false, Message = $"Unknown command: {request.Command}" }
            };
        }

        // Synchronous command handlers
        private ControlResponse HandleStatusCommand()
        {
            var process = Environment.ProcessId;
            var uptime = DateTime.Now - _startupTimestamp.GetValueOrDefault(DateTime.Now);

            return new ControlResponse
            {
                Success = true,
                Message = "Daemon is running",
                Data = new StatusInformation
                {
                    Status = "Running",
                    ProcessId = process,
                    Uptime = uptime,
                    TimeStamp = DateTime.UtcNow
                }
            };
        }

        private ControlResponse HandleStopCommand()
        {
            _logger.LogInformation("Stop command received");

            var response = new ControlResponse
            {
                Success = true,
                Message = "Daemon shutdown initiated",
            };

            // Start shutdown in separate thread to avoid blocking response
            var shutdownThread = new Thread(() =>
            {
                try
                {
                    Thread.Sleep(100); // Ensure response is sent
                    _applicationLifetime.StopApplication();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during shutdown process");
                }
            });

            shutdownThread.IsBackground = true;
            shutdownThread.Start();

            return response;
        }

        private ControlResponse HandleSyncCommand(Dictionary<string, object>? parameters)
        {
            // Implement sync logic
            return new ControlResponse
            {
                Success = true,
                Message = "Sync initiated"
            };
        }

        // Placeholder handlers - make these synchronous too
        private ControlResponse HandleListCommand()
        {
            return new ControlResponse
            {
                Success = true,
                Message = "List command received"
            };
        }

        private ControlResponse HandleRemoveCommand(Dictionary<string, object>? parameters)
        {
            return new ControlResponse
            {
                Success = true,
                Message = "Remove command received"
            };
        }

        private ControlResponse HandleCreateCommand(Dictionary<string, object>? parameters)
        {
            return new ControlResponse
            {
                Success = true,
                Message = "Create command received"
            };
        }

        private ControlResponse HandleVerifyCommand(Dictionary<string, object>? parameters)
        {
            return new ControlResponse
            {
                Success = true,
                Message = "Verify command received"
            };
        }

        private ControlResponse HandleFetchCommand(Dictionary<string, object>? parameters)
        {
            return new ControlResponse
            {
                Success = true,
                Message = "Fetch command received"
            };
        }

        private ControlResponse HandleAddCommand(Dictionary<string, object>? parameters)
        {
            return new ControlResponse
            {
                Success = true,
                Message = "Add command received"
            };
        }

        private ControlResponse HandlePopCommand(Dictionary<string, object>? parameters)
        {
            return new ControlResponse
            {
                Success = true,
                Message = "Pop command received"
            };
        }

        private ControlResponse HandleLedgerCommand(Dictionary<string, object>? parameters)
        {
            return new ControlResponse
            {
                Success = true,
                Message = "Ledger command received"
            };
        }
    }
}