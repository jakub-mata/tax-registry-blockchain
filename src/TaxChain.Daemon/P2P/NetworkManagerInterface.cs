using System;
using System.Threading;
using System.Threading.Tasks;

namespace TaxChain.Daemon.P2P;

public interface INetworkManaging : IDisposable
{
    Task StartAsync(int port, int discoveryDelay = 30, CancellationToken ct = default);
    void AddKnownPeer(string host, int port);
    Tuple<bool, DateTime> GetStatus();
    Task SyncChain(Guid chainId, CancellationToken ct = default);
}