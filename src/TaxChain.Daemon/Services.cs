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
using System.Globalization;

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
                "gather" => HandleGatherCommand(request.Parameters),
                "ledger" => HandleLedgerCommand(request.Parameters),
                _ => new ControlResponse { Success = false, Message = $"Unknown command: {request.Command}" }
            };
        }

        private ControlResponse HandleGatherCommand(Dictionary<string, object>? parameters)
        {
            if (parameters == null)
            {
                return new ControlResponse
                {
                    Success = false,
                    Message = "Client failed to provide necessary parameters, namely [int]taxpayerId",
                };
            }
            bool ok = parameters.TryGetValue("taxpayerId", out object? value);
            if (!ok || value == null)
            {
                return new ControlResponse
                {
                    Success = false,
                    Message = "Client failed to provide necessary parameters, namely [int]taxpayerId",
                };
            }
            ok = parameters.TryGetValue("chainId", out object? id);
            if (!ok || id == null)
            {
                return new ControlResponse
                {
                    Success = false,
                    Message = "Client failed to provide necessary parameters, namely [Guid]chainId",
                };
            }
            ok = parameters.TryGetValue("verbose", out object? v);
            bool verbose = ok && (v != null);
            try
            {
                int payerId = (int)value;
                Guid chainId = (Guid)id;
                ok = _blockchainRepository.GatherTaxpayer(chainId, payerId, out List<Transaction> trans);
                if (!ok)
                {
                    return new ControlResponse
                    {
                        Success = false,
                        Message = "Repository failed to gather taxpayer information",
                    };
                }
                return new ControlResponse
                {
                    Success = true,
                    Message = "Successfully retrieved taxpayer information",
                    Data = trans,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError("Exception during gather command processing: {ex}", ex);
                return new ControlResponse
                {
                    Success = false,
                    Message = $"Exception occured during processing: {ex}",
                };
            }
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

        private ControlResponse HandleListCommand()
        {
            _logger.LogInformation("List command recevied...");
            try
            {
                bool ok = _blockchainRepository.ListChains(out List<Blockchain> chains);
                if (!ok)
                {
                    _logger.LogInformation("Repository failed to retrieve local chains");
                    return new ControlResponse
                    {
                        Success = false,
                        Message = "Repository failed to retrive local chains",
                    };
                }
                return new ControlResponse
                {
                    Success = true,
                    Message = "List command successful",
                    Data = chains,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError("Exception during list command processing: {ex}", ex);
                return new ControlResponse
                {
                    Success = false,
                    Message = $"Exception occured during processing: {ex}",
                };
            }
        }

        private ControlResponse HandleRemoveCommand(Dictionary<string, object>? parameters)
        {
            if (parameters == null)
            {
                return new ControlResponse
                {
                    Success = false,
                    Message = "Client failed to provide necessary parameters, namely '[Guid]chainId'",
                };
            }
            bool ok = parameters.TryGetValue("chainId", out object? chainId);
            if (!ok || chainId == null)
            {
                return new ControlResponse
                {
                    Success = false,
                    Message = "Client failed to provide necessary parameters, namely '[Guid]chainId'",
                };
            }
            try
            {
                Guid id = (Guid)chainId;
                ok = _blockchainRepository.RemoveChain(id);
                if (!ok)
                {
                    return new ControlResponse
                    {
                        Success = false,
                        Message = $"Repository failed to delete local blockchain with id {id.ToString()}",
                    };
                }
                return new ControlResponse
                {
                    Success = true,
                    Message = "Removal successful",
                };
            }
            catch (Exception ex)
            {
                _logger.LogError("Exception during remove command processing: {ex}", ex);
                return new ControlResponse
                {
                    Success = false,
                    Message = $"Exception occured during processing: {ex}",
                };
            }
        }

        private ControlResponse HandleCreateCommand(Dictionary<string, object>? parameters)
        {
            if (parameters == null)
            {
                return new ControlResponse
                {
                    Success = false,
                    Message = "Client failed to provide necessary parameters, namely '[Blockchain]blockchain'",
                };
            }
            bool ok = parameters.TryGetValue("blockchain", out object? chain);
            if (!ok || chain == null)
            {
                return new ControlResponse
                {
                    Success = false,
                    Message = "Client failed to provide necessary parameters, namely '[Blockchain]blockchain'",
                };
            }
            try
            {
                Blockchain b = (Blockchain)chain;
                ok = _blockchainRepository.Store(b);
                if (!ok)
                {
                    return new ControlResponse
                    {
                        Success = false,
                        Message = "Repository failed to create a new chain",
                    };
                }
                return new ControlResponse
                {
                    Success = true,
                    Message = "Successfully created a new chain"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError("Exception during create command processing: {ex}", ex);
                return new ControlResponse
                {
                    Success = false,
                    Message = $"Exception occured during processing: {ex}",
                };
            }
        }

        private ControlResponse HandleVerifyCommand(Dictionary<string, object>? parameters)
        {
            if (parameters == null)
            {
                return new ControlResponse
                {
                    Success = false,
                    Message = "Client failed to provide necessary parameters, namely '[Guid]chainId'",
                };        
            }
            bool ok = parameters.TryGetValue("chainId", out object? id);
            if (!ok || id == null)
            {
                return new ControlResponse
                {
                    Success = false,
                    Message = "Client failed to provide necessary parameters, namely '[Guid]chainId'",
                };  
            }
            try
            {
                Guid chainId = (Guid)id;
                ok = _blockchainRepository.Verify(chainId);
                if (!ok)
                {
                    return new ControlResponse
                    {
                        Success = false,
                        Message = "The chain is invalid.",
                    };
                }
                return new ControlResponse
                {
                    Success = true,
                    Message = "The chain is valid and not tampered with."
                };
            }
            catch (System.Exception ex)
            {
                _logger.LogError("Exception during verify command processing: {ex}", ex);
                return new ControlResponse
                {
                    Success = false,
                    Message = $"Exception occured during processing: {ex}",
                };
            }
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
            if (parameters == null)
            {
                return new ControlResponse
                {
                    Success = false,
                    Message = "Client failed to provide necessary parameters, namely '[Transaction]transaction'",
                };          
            }
            bool ok = parameters.TryGetValue("transaction", out object? t);
            if (!ok || t == null)
            {
                return new ControlResponse
                {
                    Success = false,
                    Message = "Client failed to provide necessary parameters, namely '[Transaction]transaction'",
                };
            }
            ok = parameters.TryGetValue("chainId", out object? id);
            if (!ok || id == null)
            {
                return new ControlResponse
                {
                    Success = false,
                    Message = "Client failed to provide necessary parameters, namely '[Guid]chainId'",
                };
            }
            try
            {
                Transaction transaction = (Transaction)t;
                Guid chainId = (Guid)id;
                ok = _blockchainRepository.EnqueueTransaction(chainId, transaction);
                if (!ok)
                {
                    return new ControlResponse
                    {
                        Success = false,
                        Message = "Repository failed to add transaction.",
                    };
                }
                return new ControlResponse
                {
                    Success = true,
                    Message = "Successfully added transaction",
                };
            }
            catch (System.Exception ex)
            {
                _logger.LogError("Exception during add command processing: {ex}", ex);
                return new ControlResponse
                {
                    Success = false,
                    Message = $"Exception occured during processing: {ex}",
                };
            }
        }

        private ControlResponse HandleLedgerCommand(Dictionary<string, object>? parameters)
        {
            if (parameters == null)
            {
                return new ControlResponse
                {
                    Success = false,
                    Message = "Client failed to provide necessary parameters, namely '[Guid]chainId'",
                };
            }
            bool ok = parameters.TryGetValue("chainId", out object? id);
            if (!ok || id == null)
            {
                return new ControlResponse
                {
                    Success = false,
                    Message = "Client failed to provide necessary parameters, namely '[Guid]chainId'",
                };
            }
            ok = parameters.TryGetValue("number", out object? n);
            if (!ok || n == null)
            {
                return new ControlResponse
                {
                    Success = false,
                    Message = "Client failed to provide necessary parameters, namely '[int]number'",
                };  
            }
            try
            {
                Guid chainId = (Guid)id;
                int number = (int)n;
                ok = _blockchainRepository.Tail(chainId, number, out Block[] blocks);
                if (!ok)
                {
                    return new ControlResponse
                    {
                        Success = false,
                        Message = "Repository failed to fetch chain tail",
                    };
                }
                return new ControlResponse
                {
                    Success = true,
                    Message = "Successfully fetched chain tail",
                    Data = blocks,
                };
            }
            catch (System.Exception ex)
            {
                _logger.LogError("Exception during ledger command processing: {ex}", ex);
                return new ControlResponse
                {
                    Success = false,
                    Message = $"Exception occured during processing: {ex}",
                };
            }
        }
    }
}