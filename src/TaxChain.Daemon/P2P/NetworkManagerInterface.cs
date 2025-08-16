using System;
using System.Threading.Tasks;

namespace TaxChain.P2P;

public interface INetworkManaging : IDisposable
{
    Task SyncChain(Guid chainId);
}