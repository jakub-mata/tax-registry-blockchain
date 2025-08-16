using System.Net;
using System.Net.Sockets;
using TaxChain.P2P.Messages;
using TaxChain.Daemon.Storage;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;
using TaxChain.core;

namespace TaxChain.P2P;

public class P2PNode : IDisposable
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
        catch (OperationCanceledException) { }
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

    public async Task ConnectToPeerAsync(string host, int port, Guid chainId, CancellationToken ct = default)
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
    private async Task HandleChainInfoRequest(P2PMessage msg, PeerConnection peer, CancellationToken ct)
    {
        var ci = msg.Deserialize<ChainInfo>();
        bool ok = _repo.GetBlockchain(ci.ChainId, out Blockchain? b);
        if (!ok)
        {
            throw new Exception("Failed to fetch blockchain needed by peer");
        }
        if (!b.HasValue)
        {
            throw new Exception("Blockchain provided by peer does not exist");
        }

        ok = _repo.Fetch(ci.ChainId, out List<Block> blocks);
        if (!ok)
        {
            throw new Exception("Failed to fetch all blocks for peer");
        }
        await peer.SendAsync("Blocks", new Blocks(b.Value, blocks), ct);
    }

    private void HandleBlocksRequest(P2PMessage msg)
    {
        var blockMsg = msg.Deserialize<Blocks>();
        bool ok = _repo.GetBlockchain(blockMsg.Blockchain.Id, out Blockchain? b);
        if (!ok)
        {
            throw new Exception("Failed to retrieve a blockchain");
        }
        if (!b.HasValue)
        {
            throw new Exception("Blockchain provided by peer does not exist...");
        }
        if (b.Value.Difficulty == blockMsg.Blockchain.Difficulty)
        {
            throw new Exception("Difficulty of chain does not match");
        }
        bool valid = P2PUtils.ValidateBlocks(blockMsg.ChainBlocks, blockMsg.Blockchain.Difficulty);
        if (!valid)
        {
            throw new Exception("Blocks provided by peer are not valid");
        }
        ok = _repo.ReplaceChainBlocks(blockMsg.Blockchain.Id, blockMsg.ChainBlocks);
        if (!ok)
        {
            throw new Exception("Failed to replace the given blockchain for peer");
        }
    }
}
