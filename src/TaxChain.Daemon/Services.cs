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
using System.Reflection;

namespace TaxChain.Daemon.Services
{
    /// <summary>
    /// ControlService is responsible for managing the lifecycle of the daemon,
    /// including starting and stopping the network, handling commands from clients,
    /// and performing various operations on the blockchain.
    /// </summary>
    public class ControlService : IHostedService
    {
        private readonly IBlockchainRepository _blockchainRepository;
        private readonly INetworkManaging _networkManager;
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly ILogger<ControlService> _logger;
        private int? _port;
        private DateTime? _startupTimestamp;
        private Task? _synchronizationTask;
        private CancellationTokenSource _cancellationTokenSource = new();
        private CancellationTokenSource? _miningCts;
        private int _running;
        private Thread? _listeningThread;

        public ControlService(
            INetworkManaging networkManager,
            IBlockchainRepository blockchainRepository,
            IHostApplicationLifetime applicationLifetime,
            ILogger<ControlService> logger)
        {
            _networkManager = networkManager;
            _blockchainRepository = blockchainRepository;
            _applicationLifetime = applicationLifetime;
            _logger = logger;
            _running = 0;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _startupTimestamp = DateTime.Now;
            _listeningThread = new Thread(ListenForCommands);
            _listeningThread.IsBackground = false; // Keep thread alive
            _listeningThread.Start();
            _logger.LogInformation($"Control service started on pid {Environment.ProcessId}");
            _logger.LogInformation($"Initializing storage...");
            _blockchainRepository.Initialize();
            _logger.LogInformation("Storage successfully initialized");
            _logger.LogInformation("Booting up networking...");
            var port = Environment.GetEnvironmentVariable("RECEIVER_PORT") ?? "4662";
            _port = int.Parse(port);
            var discoveryInterval = Environment.GetEnvironmentVariable("DISCOVERY_INTERVAL") ?? "60";
            _networkManager.StartAsync(int.Parse(port), int.Parse(discoveryInterval), cancellationToken);
            _logger.LogInformation("Networking set up successfully, starting chain synchronization...");
            _synchronizationTask = SyncLoop(60, cancellationToken);
            Program.VerboseMode = false;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            CancelMining();
            _networkManager.Dispose();
            Thread.Sleep(300);
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

        private async Task SynchronizeOurChains(CancellationToken ct = default)
        {
            bool ok = _blockchainRepository.ListChains(out List<Blockchain> chains);
            if (!ok)
            {
                _logger.LogError("Failed to fetch all chains for sync at startup");
                return;
            }
            foreach (Blockchain chain in chains)
            {
                _logger.LogInformation("Syncing chain {id}...", chain.Id.ToString());
                await _networkManager.SyncChain(chain.Id, ct);
            }
            _logger.LogInformation("Finished syncing.");
        }

        private async Task SyncLoop(int delayInSec = 60, CancellationToken ct = default)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await SynchronizeOurChains(ct);
                    await Task.Delay(TimeSpan.FromSeconds(delayInSec), ct);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Operation cancelled in discovery");
            }
            catch (Exception ex)
            {
                _logger.LogError("Exception in discovery: {ex}", ex);
            }
        }

        private async void ListenForCommands()
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

                    await HandleClientConnection(pipeServer);
                    _logger.LogInformation("Client connection handled");
                    Program.VerboseMode = false;
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

        private async Task HandleClientConnection(NamedPipeServerStream pipe)
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
                        var response = await ProcessCommand(request);
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
            catch (IOException)
            {
                _logger.LogInformation("Closed client connection");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling client connection");
            }
        }

        private async Task<ControlResponse> ProcessCommand(ControlRequest? request)
        {
            if (request == null)
                return new ControlResponse { Success = false, Message = "Invalid request" };

            if (request.Parameters != null &&
                request.Parameters.TryGetValue("verbose", out object? verboseValue) &&
                verboseValue is JsonElement jsonElementVerbose)
            {
                bool verbose = false;
                try
                {
                    verbose = JsonSerializer.Deserialize<bool>(jsonElementVerbose.GetRawText());
                }
                catch
                { }
                Program.VerboseMode = verbose;
                _logger.LogInformation("Verbose logging mode set to: {mode}", verbose);
            }

            _logger.LogInformation($"Processing command: {request.Command}");

            return request.Command.ToLower() switch
            {
                "status" => HandleStatusCommand(),
                "stop" => HandleStopCommand(),
                "sync" => await HandleSyncCommand(_cancellationTokenSource.Token),
                "list" => HandleListCommand(),
                "remove" => HandleRemoveCommand(request.Parameters),
                "create" => HandleCreateCommand(request.Parameters),
                "verify" => HandleVerifyCommand(request.Parameters),
                "fetch" => await HandleFetchCommand(request.Parameters, _cancellationTokenSource.Token),
                "add" => HandleAddCommand(request.Parameters),
                "gather" => HandleGatherCommand(request.Parameters),
                "ledger" => HandleLedgerCommand(request.Parameters),
                "mine" => HandleMineCommand(request.Parameters),
                "info" => HandleInfoCommand(request.Parameters),
                "connect" => HandleConnectCommand(request.Parameters),
                _ => new ControlResponse { Success = false, Message = $"Unknown command: {request.Command}" }
            };
        }

        private ControlResponse HandleConnectCommand(Dictionary<string, object>? parameters)
        {
            if (parameters == null)
            {
                _logger.LogWarning("Client has not provided necessary parameters, namely [string]host");
                return new ControlResponse
                {
                    Success = false,
                    Message = "No [string]host parameter"
                };
            }
            bool ok = parameters.TryGetValue("host", out object? hostValue);
            if (!ok || hostValue == null)
            {
                _logger.LogWarning("Client has not provided necessary parameters, namely [string]host");
                return new ControlResponse
                {
                    Success = false,
                    Message = "No [string]host parameter"
                };
            }
            ok = parameters.TryGetValue("port", out object? portValue);
            if (!ok || portValue == null)
            {
                _logger.LogWarning("Client has not provided necessary parameters, namely [int]port");
                return new ControlResponse
                {
                    Success = false,
                    Message = "No [int]port parameter"
                };
            }

            try
            {
                string? host = (hostValue is JsonElement jsonElement)
                    ? JsonSerializer.Deserialize<string>(jsonElement.GetRawText())
                    : (string?)hostValue;
                int? port = (portValue is JsonElement jsonElement1)
                    ? JsonSerializer.Deserialize<int>(jsonElement1.GetRawText())
                    : (int?)portValue;
                if (host == null || !port.HasValue)
                {
                    _logger.LogWarning("Failed to convert parameters");
                    return new ControlResponse
                    {
                        Success = false,
                        Message = "Failed to convert provided parameters"
                    };
                }
                _networkManager.AddKnownPeer(host, port.Value);
                return new ControlResponse
                {
                    Success = true,
                    Message = "Successfully added the peer"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError("Exception during connect command: {ex}", ex);
                return new ControlResponse
                {
                    Success = false,
                    Message = $"Exception: {ex}",
                };
            }
        }

        private ControlResponse HandleInfoCommand(Dictionary<string, object>? parameters)
        {
            if (parameters == null)
            {
                return new ControlResponse
                {
                    Success = false,
                    Message = "Client failed to provide necessary parameters, namely [Guid]chainId"
                };
            }
            bool ok = parameters.TryGetValue("chainId", out object? value);
            if (!ok || value == null)
            {
                return new ControlResponse
                {
                    Success = false,
                    Message = "Client failed to provide necessary parameters, namely [Guid]chainId"
                };
            }
            try
            {
                Guid chainId = (value is JsonElement jsonElement)
                    ? JsonSerializer.Deserialize<Guid>(jsonElement)
                    : (Guid)value;
                ok = _blockchainRepository.GetBlockchain(chainId, out Blockchain? b);
                if (!ok)
                {
                    return new ControlResponse
                    {
                        Success = false,
                        Message = "Daemon failed to get blockchain from the storage",
                    };
                }
                if (!b.HasValue)
                {
                    return new ControlResponse
                    {
                        Success = false,
                        Message = "No such blockchain found",
                    };
                }
                return new ControlResponse
                {
                    Success = true,
                    Message = "Blockchain info fetched",
                    Data = b
                };
            }
            catch (Exception ex)
            {
                _logger.LogError("Exception during info command: {ex}", ex);
                return new ControlResponse
                {
                    Success = false,
                    Message = $"Exception: {ex}",
                };
            }
        }

        private ControlResponse HandleMineCommand(Dictionary<string, object>? parameters)
        {
            if (parameters == null)
            {
                _logger.LogWarning("Client failed to provide necessary parameters, namely [Guid]chainId");
                return new ControlResponse
                {
                    Success = false,
                    Message = "Missing parameters",
                };
            }
            bool ok = parameters.TryGetValue("chainId", out object? chainIdObject);
            if (!ok || chainIdObject == null)
            {
                _logger.LogWarning("Client failed to provide necessary parameters, namely [Guid]chainId");
                return new ControlResponse
                {
                    Success = false,
                    Message = "Missing parameter chainId",
                };
            }
            ok = parameters.TryGetValue("rewardAddress", out object? rewardObject);
            if (!ok || rewardObject == null)
            {
                _logger.LogWarning("Client failed to provide necessary parameters, namely [string]rewardAddress");
                return new ControlResponse
                {
                    Success = false,
                    Message = "Missing parameter rewardAddress",
                };
            }
            try
            {
                Guid chainId = (chainIdObject is JsonElement jsonElement)
                    ? JsonSerializer.Deserialize<Guid>(jsonElement.GetRawText())
                    : (Guid)chainIdObject;
                string? taxpayerId = (rewardObject is JsonElement jsonElement1)
                    ? JsonSerializer.Deserialize<string>(jsonElement1.GetRawText())
                    : (string?)rewardObject;
                ok = _blockchainRepository.FetchPending(chainId, out Transaction? transaction);
                if (!ok)
                {
                    _logger.LogWarning("Failed to fetch pending transactions");
                    return new ControlResponse
                    {
                        Success = false,
                        Message = "Failed to fetch pending transactions",
                    };
                }
                if (!transaction.HasValue)
                {
                    _logger.LogInformation("No pending transactions to mine");
                    return new ControlResponse
                    {
                        Success = true,
                        Message = "No pending transactions",
                    };
                }
                ok = _blockchainRepository.Tail(chainId, 1, out List<Block> lastBlock);
                if (!ok || lastBlock.Count != 1)
                {
                    _logger.LogWarning("Failed to fetch last block");
                    return new ControlResponse
                    {
                        Success = false,
                        Message = "Failed to fetch last block for previous hash",
                    };
                }
                // fetch difficulty
                ok = _blockchainRepository.GetBlockchain(chainId, out Blockchain? b);
                if (!ok || !b.HasValue)
                {
                    _logger.LogWarning("Failed to retrieve blockchain from storage, stopping mining...");
                    return new ControlResponse
                    {
                        Success = false,
                        Message = "Failed to retrieve blockchain from storage, stopping mining...",
                    };
                }
                Block toMine = new(chainId, lastBlock[0].Hash, transaction.Value);
                HandleMiningWorker(toMine, b.Value.Difficulty, taxpayerId!, b.Value.RewardAmount);
                return new ControlResponse
                {
                    Success = true,
                    Message = "Mining has started!",
                };
            }
            catch (InvalidOperationException)
            {
                _logger.LogWarning("Mining worker is already busy...");
                return new ControlResponse
                {
                    Success = false,
                    Message = "Already mining, try again later..."
                };
            }
            catch (OverflowException)
            {
                _logger.LogError("Impossible to mine the block, tried all nonce values...");
                return new ControlResponse
                {
                    Success = false,
                    Message = "Impossible to mine the block",
                };
            }
            catch (Exception ex)
            {
                _logger.LogError("Exception during mine command: {ex}", ex);
                return new ControlResponse
                {
                    Success = false,
                    Message = $"Exception: {ex}",
                };
            }
        }

        private void HandleMiningWorker(Block toMine, int difficulty, string taxpayerId, float rewardAmount)
        {
            if (Interlocked.Exchange(ref this._running, 1) == 1)
                throw new InvalidOperationException("Worker already running");
            _miningCts = new CancellationTokenSource();
            var token = _miningCts.Token;

            Thread thread = new Thread(() =>
            {
                try
                {
                    toMine.Mine(difficulty, token);
                    _logger.LogInformation("Mining successful! Storing mined block...");
                    AppendResult result = _blockchainRepository.AppendBlock(toMine);
                    if (result != AppendResult.Success)
                    {
                        _logger.LogError("Failed to store new block");
                        return;
                    }
                    _logger.LogInformation("Mined block stored. Sending reward...");
                    Transaction reward = new(taxpayerId, rewardAmount, TaxType.Reward);
                    Block rewardBlock = new(toMine.ChainId, toMine.Hash, reward);
                    rewardBlock.Mine(difficulty, token);
                    result = _blockchainRepository.AppendBlock(rewardBlock, true);
                    if (result != AppendResult.Success)
                    {
                        _logger.LogError("Failed to store the reward block");
                        return;
                    }
                    _logger.LogInformation("Reward block stored.");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Mining has been canceled");
                }
                catch (Exception ex)
                {
                    _logger.LogError("Exception during mining: {ex}", ex);
                }
                finally
                {
                    Interlocked.Decrement(ref this._running);
                }
            });
            thread.Start();
        }

        private void CancelMining()
        {
            _miningCts?.Cancel();
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

            try
            {
                string? payerId = (value is JsonElement jsonElement)
                    ? JsonSerializer.Deserialize<string>(jsonElement)
                    : (string?)value;
                if (payerId == null)
                    return new ControlResponse
                    {
                        Success = false,
                        Message = "Invalid taxpayer Id",
                    };
                Guid chainId = (id is JsonElement jsonElement1)
                    ? JsonSerializer.Deserialize<Guid>(jsonElement1)
                    : (Guid)id;
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

        private ControlResponse HandleStatusCommand()
        {
            var process = Environment.ProcessId;
            var uptime = DateTime.Now - _startupTimestamp.GetValueOrDefault(DateTime.Now);
            Tuple<bool, DateTime> status = _networkManager.GetStatus();

            return new ControlResponse
            {
                Success = true,
                Message = "Daemon is running",
                Data = new StatusInformation
                {
                    Status = "Running",
                    ProcessId = process,
                    Uptime = uptime,
                    TimeStamp = DateTime.UtcNow,
                    Mining = _running == 1,
                    Port = _port ?? 0,
                    SyncSuccess = status.Item1,
                    SyncLast = status.Item2,
                    ConnectedPeers = _networkManager.CountPeers(),
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

        private async Task<ControlResponse> HandleSyncCommand(CancellationToken ct = default)
        {
            try
            {
                await SynchronizeOurChains(ct);
                // Implement sync logic
                return new ControlResponse
                {
                    Success = true,
                    Message = "Sync successful"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to sync local chains: {ex}", ex);
                return new ControlResponse
                {
                    Success = false,
                    Message = $"Exception during sync processing: {ex.ToString()}"
                };
            }
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
                Guid id = (chainId is JsonElement jsonElement)
                    ? JsonSerializer.Deserialize<Guid>(jsonElement.GetRawText())
                    : (Guid)chainId;
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
                Blockchain b = (chain is JsonElement jsonElement)
                    ? JsonSerializer.Deserialize<Blockchain>(jsonElement.GetRawText())
                    : (Blockchain)chain;

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
                    Message = "Successfully created a new chain",
                    Data = b.Id,
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
                Guid chainId = (id is JsonElement jsonElement)
                    ? JsonSerializer.Deserialize<Guid>(jsonElement)
                    : (Guid)id;
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

        private async Task<ControlResponse> HandleFetchCommand(Dictionary<string, object>? parameters, CancellationToken ct = default)
        {
            if (parameters == null)
            {
                _logger.LogWarning("Client has not provided parameters, aborting fetch...");
                return new ControlResponse
                {
                    Success = false,
                    Message = "Client has not provided requested parameters, namely [Guid]chainId"
                };
            }
            bool ok = parameters.TryGetValue("chainId", out object? value);
            if (!ok || value == null)
            {
                _logger.LogWarning("Client has not provided parameters, aborting fetch...");
                return new ControlResponse
                {
                    Success = false,
                    Message = "Client has not provided requested parameters, namely [Guid]chainId"
                };
            }

            try
            {
                Guid chainId = (value is JsonElement jsonElement)
                    ? JsonSerializer.Deserialize<Guid>(jsonElement.GetRawText())
                    : (Guid)value;
                await _networkManager.SyncChain(chainId, default);
                return new ControlResponse
                {
                    Success = true,
                    Message = "Fetch command processed successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError("Exception during fetch: {ex}", ex);
                return new ControlResponse
                {
                    Success = false,
                    Message = $"Exception during fetch processing: {ex.ToString()}"
                };
            }
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
                Transaction transaction = (t is JsonElement jsonElement)
                    ? JsonSerializer.Deserialize<Transaction>(jsonElement.GetRawText())
                    : (Transaction)t;
                Guid chainId = (id is JsonElement jsonElement1)
                    ? JsonSerializer.Deserialize<Guid>(jsonElement1.GetRawText())
                    : (Guid)id;
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
                Guid chainId = (id is JsonElement jsonElement)
                    ? JsonSerializer.Deserialize<Guid>(jsonElement)
                    : (Guid)id;
                int number = (n is JsonElement jsonElement1)
                    ? JsonSerializer.Deserialize<int>(jsonElement1)
                    : (int)n;
                ok = _blockchainRepository.Tail(chainId, number, out List<Block> blocks);
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