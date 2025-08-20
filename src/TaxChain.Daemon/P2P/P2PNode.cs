using System.Net;
using System.Net.Sockets;
using TaxChain.Daemon.P2P.Messages;
using TaxChain.Daemon.Storage;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;
using TaxChain.core;
using System.Linq;
using System.IO;

namespace TaxChain.Daemon.P2P;

/// <summary>
/// Represents a P2P node that manages peer connections and facilitates blockchain synchronization.
/// It handles discovery of peers, connection management, and synchronization of blockchain data.
/// </summary>
public class P2PNode : IDisposable, INetworkManaging
{
    private readonly IBlockchainRepository _repo;
    private readonly HashSet<PeerConnection> _peers = new();
    private readonly HashSet<IPEndPoint> _knownPeers = new();
    private readonly Guid _localId = Guid.NewGuid();
    private readonly ILogger<P2PNode> _logger;
    public SyncStatus Status { get; set; }
    private int _discoveryDelay = 30;
    private Task? _discoveryTask;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public P2PNode(IBlockchainRepository repo, ILogger<P2PNode> logger)
    {
        _repo = repo;
        _logger = logger;
        Status = new SyncStatus { Success = true, DateTime = DateTime.UtcNow };
    }

    public async Task StartAsync(int port, int discoveryDelay = 30, CancellationToken ct = default)
    {
        _discoveryDelay = discoveryDelay;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        _ = AcceptLoop(_cts.Token);

        _discoveryTask = DiscoveryLoop(_cts.Token);
    }

    public async Task StopAsync()
    {
        _logger.LogInformation("Initiating graceful shutdown of P2P Node");
        _cts?.Cancel();
        if (_discoveryTask != null)
            await _discoveryTask;
        _listener?.Stop();
        foreach (var peer in _peers)
            peer.Dispose();
        _peers.Clear();
        _logger.LogInformation("Shutdown complete");
    }

    public Tuple<bool, DateTime> GetStatus() => new Tuple<bool, DateTime>(Status.Success, Status.DateTime);

    public int CountPeers() => _peers.Count;

    public void AddKnownPeer(string host, int port)
    {
        var ip = Dns.GetHostAddresses(host).First();
        _knownPeers.Add(new IPEndPoint(ip, port));
    }

    public async Task SyncChain(Guid chainId, CancellationToken ct = default)
    {
        foreach (var peer in _peers.ToList())
        {
            try
            {
                Console.WriteLine($"Syncing chain {chainId} from peer {peer.PeerId}");
                _logger.LogInformation("Syncing chain {ChainId} from peer {Peer}", chainId, peer.PeerId);
                bool ok = _repo.CountBlocks(chainId, out int blockCount);
                if (!ok)
                    throw new Exception("Failed to fetch blockchain count");
                Console.WriteLine($"Sending chain info message");
                await peer.SendAsync("ChainInfo", new ChainInfo(chainId, blockCount), ct);

                // Blocks response comes in HandlePeer
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed syncing with peer {Peer}: {ex}", peer.PeerId, ex.Message);
                _peers.Remove(peer);
                peer.Dispose();
            }
        }
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    private async Task DiscoveryLoop(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                bool ok = true;
                foreach (var endpoint in _knownPeers.ToList())
                {
                    if (_peers.Any(p => p.RemoteEndPoint!.Equals(endpoint)))
                        continue; // Already checked
                    try
                    {
                        _logger.LogInformation("Attempting discovery connect to {Endpoint}", endpoint);
                        await ConnectToPeerAsync(endpoint, ct);
                    }
                    catch (Exception ex)
                    {
                        ok = false;
                        _logger.LogWarning("Failed to connect to {Endpoint}: {ex}", endpoint, ex.Message);
                    }
                }
                Status = new SyncStatus { Success = ok, DateTime = DateTime.UtcNow };
                await Task.Delay(TimeSpan.FromSeconds(_discoveryDelay), ct);
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

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var client = await _listener!.AcceptTcpClientAsync(ct);
            var peer = new PeerConnection(client);
            Console.WriteLine("Received a request");
            try
            {
                await peer.PerformServerHandshake(_localId, ct);
                if (_peers.Add(peer))
                {
                    _ = HandlePeer(peer, ct);

                    // After handshake, share peers for discovery
                    await peer.SendAsync("PeerList", new PeerListMessage(_knownPeers.Select(p => p.ToString()).ToList()), ct);
                }
                else
                {
                    peer.Dispose();
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                _logger.LogWarning("Handling of peer failed: {ex}", ex);
            }
        }
    }

    private async Task HandlePeer(PeerConnection peer, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var msg = await peer.ReceiveAsync(ct);
                if (msg == null) break;

                switch (msg.Type)
                {
                    case "ChainInfo":
                        Console.WriteLine("received chaininfo request");
                        await HandleChainInfoRequest(msg, peer, ct);
                        break;
                    case "Blocks":
                        Console.WriteLine("Blocks response received");
                        HandleBlocksRequest(msg);
                        break;
                    case "PeerList":
                        HandlePeerList(msg);
                        break;
                }
            }
        }
        catch (EndOfStreamException)
        {
            _logger.LogInformation("End of stream while peer handling...");
        }
        catch (Exception ex)
        {
            _logger.LogError("Exception during peer handling: {ex}", ex);
        }
        finally
        {
            peer.Dispose();
            _peers.Remove(peer);
        }
    }

    private async Task ConnectToPeerAsync(IPEndPoint endpoint, CancellationToken ct = default)
    {
        _logger.LogInformation("Attempting to connect to peer: {peer}", endpoint.Address);
        var client = new TcpClient();
        await client.ConnectAsync(endpoint, ct);
        var peer = new PeerConnection(client);
        await peer.PerformClientHandshake(_localId, ct);

        // Sync our peer list
        if (_peers.Add(peer))
        {
            _ = HandlePeer(peer, ct);

            // Send our known peers for discovery
            await peer.SendAsync("PeerList", new PeerListMessage(_knownPeers.Select(p => p.ToString()).ToList()), ct);
        }
        else
        {
            peer.Dispose();
        }
    }


    /*--- MESSAGE HANDLERS ---*/
    private async Task<bool> HandleChainInfoRequest(P2PMessage msg, PeerConnection peer, CancellationToken ct)
    {
        var ci = msg.Deserialize<ChainInfo>();
        Console.WriteLine("Deserialized chain info request");
        bool ok = _repo.GetBlockchain(ci.ChainId, out Blockchain? b);
        if (!ok)
        {
            _logger.LogError("Failed to fetch blockchain needed by peer");
            return false;
        }
        if (!b.HasValue)
        {
            _logger.LogWarning("Blockchain provided by peer does not exist");
            return false;
        }
        Console.WriteLine("Found blockchain");
        ok = _repo.CountBlocks(ci.ChainId, out int blockCount);
        if (!ok || blockCount <= ci.BlockCount)
        {
            _logger.LogInformation("Our blockchain is shorter, not sending any data...");
            return false;
        }
        Console.WriteLine("Counted blocks");
        ok = _repo.Fetch(ci.ChainId, out List<Block> blocks);
        if (!ok)
        {
            _logger.LogError("Failed to fetch all blocks for peer");
            return false;
        }
        Console.WriteLine("Sending 'blocks' response");
        await peer.SendAsync("Blocks", new Blocks(b.Value, blocks), ct);
        return true;
    }

    private void HandleBlocksRequest(P2PMessage? msg)
    {
        if (msg == null)
        {
            _logger.LogWarning("Msg is null when handling, aborting...");
            return;
        }

        var blockMsg = msg.Deserialize<Blocks>();
        if (blockMsg.ChainBlocks.Count == 0)
        {
            _logger.LogWarning("Received a 'Blocks' message with no blocks. Assuming an error on peer...");
            return;
        }
        Console.WriteLine($"Got their block count: {blockMsg.ChainBlocks.Count}");
        bool ok = _repo.GetBlockchain(blockMsg.Blockchain.Id, out Blockchain? b);
        if (!ok)
        {
            _logger.LogError("Failed to retrieve a blockchain");
            return;
        }
        if (!b.HasValue)
        {
            _logger.LogInformation("Blockchain provided by peer does not exist. Creating now...");
            Console.WriteLine("Creating new blockchain");
            ok = _repo.Store(blockMsg.Blockchain);
            if (!ok)
            {
                _logger.LogError("Failed to set up a new blockchain");
                return;
            }
        }
        Console.WriteLine("Got the blockchain");
        if (b.HasValue && (b.Value.Difficulty != blockMsg.Blockchain.Difficulty))
        {
            _logger.LogWarning("Difficulty of chain does not match");
            return;
        }
        bool valid = P2PUtils.ValidateBlocks(blockMsg.ChainBlocks, blockMsg.Blockchain.Difficulty);
        if (!valid)
        {
            _logger.LogWarning("Blocks provided by peer are not valid");
            return;
        }

        ok = _repo.CountBlocks(blockMsg.Blockchain.Id, out int ourCount);
        Console.WriteLine("Counted our block count");
        if (!ok || ourCount >= blockMsg.ChainBlocks.Count)
        {
            _logger.LogInformation("No updating done, our chain is longer...");
            return;
        }
        Console.WriteLine("Replacing blocks");
        ok = _repo.ReplaceChainBlocks(blockMsg.Blockchain.Id, blockMsg.ChainBlocks);
        if (!ok)
        {
            _logger.LogError("Failed to replace the given blockchain for peer");
            return;
        }
    }

    private void HandlePeerList(P2PMessage? msg)
    {
        if (msg == null)
        {
            _logger.LogInformation("Msg is null on PeerList, aborting...");
            return;
        }

        PeerListMessage peerList = msg.Deserialize<PeerListMessage>();
        foreach (string peerAddr in peerList.Peers)
        {
            if (IPEndPoint.TryParse(peerAddr, out var endPoint))
                _knownPeers.Add(endPoint);
        }
    }
}
