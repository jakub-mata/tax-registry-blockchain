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

namespace TaxChain.Daemon.P2P;

public class P2PNode : IDisposable, INetworkManaging
{
    private readonly IBlockchainRepository _repo;
    private readonly HashSet<PeerConnection> _peers = new();
    private readonly Guid _localId = Guid.NewGuid();
    private readonly ILogger<P2PNode> _logger;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public P2PNode(IBlockchainRepository repo, ILogger<P2PNode> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task StartAsync(int port, CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        _ = AcceptLoop(_cts.Token);
    }

    public async Task StopAsync()
    {
        _logger.LogInformation("Initiating graceful shutdown of P2P Node");
        _cts?.Cancel();
        _listener?.Stop();
        foreach (var peer in _peers)
            peer.Dispose();
        _peers.Clear();
        _logger.LogInformation("Shutdown complete");
    }

    public async Task SyncChain(Guid chainId, CancellationToken ct = default)
    {
        foreach (var peer in _peers)
        {
            try
            {
                _logger.LogInformation("Syncing chain {ChainId} from peer {Peer}", chainId, peer.PeerId);
                bool ok =_repo.CountBlocks(chainId, out int blockCount);
                if (!ok)
                    throw new Exception("Failed to fetch blockchain count");
                await peer.SendAsync("ChainInfo", new ChainInfo(chainId, blockCount), ct);

                // Wait for Blocks response
                var msg = await peer.ReceiveAsync(ct);
                if (msg?.Type == "Blocks")
                {
                    HandleBlocksRequest(msg);
                    _logger.LogInformation("Successfully synced chain {ChainId}", chainId);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed syncing with peer {Peer}: {ex}", peer.PeerId, ex.Message);
            }
        }
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var client = await _listener!.AcceptTcpClientAsync(ct);
            var peer = new PeerConnection(client);
            await peer.HandshakeAsync(_localId, ct);
            _peers.Add(peer);

            try
            {
                _ = HandlePeer(peer, ct);
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
                        await HandleChainInfoRequest(msg, peer, ct);
                        break;
                    case "Blocks":
                        HandleBlocksRequest(msg);
                        break;
                }
            }
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

    [Obsolete]
    private async Task ConnectToPeerAsync(string host, int port, Guid chainId, CancellationToken ct = default)
    {
        var client = new TcpClient();
        await client.ConnectAsync(host, port, ct);
        var peer = new PeerConnection(client);
        await peer.HandshakeAsync(_localId, ct);
        _peers.Add(peer);
        _ = HandlePeer(peer, ct);

        // Ask for chain info
        bool ok = _repo.CountBlocks(chainId, out int blockCount);
        if (!ok)
        {
            throw new Exception("Failed to count blocks within a blockchain for peer");
        }
        await peer.SendAsync("ChainInfo", new ChainInfo(chainId, blockCount), ct);
    }


    /*--- MESSAGE HANDLERS ---*/
    private async Task<bool> HandleChainInfoRequest(P2PMessage msg, PeerConnection peer, CancellationToken ct)
    {
        var ci = msg.Deserialize<ChainInfo>();
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
        ok = _repo.CountBlocks(ci.ChainId, out int blockCount);
        if (!ok || blockCount < ci.BlockCount)
        {
            _logger.LogInformation("Our blockchain is shorter, not sending any data...");
            return false;
        }

        ok = _repo.Fetch(ci.ChainId, out List<Block> blocks);
        if (!ok)
        {
            _logger.LogError("Failed to fetch all blocks for peer");
            return false;
        }
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

        bool ok = _repo.GetBlockchain(blockMsg.Blockchain.Id, out Blockchain? b);
        if (!ok)
        {
            _logger.LogError("Failed to retrieve a blockchain");
            return;
        }
        if (!b.HasValue)
        {
            _logger.LogInformation("Blockchain provided by peer does not exist. Creating now...");
            ok = _repo.Store(blockMsg.Blockchain);
            if (!ok)
            {
                _logger.LogError("Failed to set up a new blockchain");
                return;
            }
        }
        if (b.HasValue && (b.Value.Difficulty == blockMsg.Blockchain.Difficulty))
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
        if (!ok || ourCount > blockMsg.ChainBlocks.Count)
        {
            _logger.LogInformation("No updating done, our chain is longer...");
            return;
        }

        ok = _repo.ReplaceChainBlocks(blockMsg.Blockchain.Id, blockMsg.ChainBlocks);
        if (!ok)
        {
            _logger.LogError("Failed to replace the given blockchain for peer");
            return;
        }
    }
}
