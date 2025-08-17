using System;
using System.Threading;
using System.Threading.Tasks;

namespace TaxChain.Daemon.P2P;

public interface INetworkManaging : IDisposable
{
    Task StartAsync(int port, CancellationToken ct = default);
    Task SyncChain(Guid chainId, CancellationToken ct = default);
}