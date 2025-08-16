using System;
using System.Threading;
using System.Threading.Tasks;

namespace TaxChain.P2P;

public interface INetworkManaging : IDisposable
{
    Task SyncChain(Guid chainId, CancellationToken ct = default);
}