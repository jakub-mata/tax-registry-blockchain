using System;
using TaxChain.core;

namespace TaxChain.Daemon.Storage;

public class PSQLRepository : IBlockchainRepository
{
    public bool EnqueueTransaction(Transaction transaction)
    {
        throw new NotImplementedException();
    }

    public bool Fetch(Guid chainId, out Block[] blocks)
    {
        throw new NotImplementedException();
    }

    public bool FetchPending(Guid chainId, out Transaction? transaction)
    {
        throw new NotImplementedException();
    }

    public bool RemoveChain(Guid chainId)
    {
        throw new NotImplementedException();
    }

    public bool RemoveLastBlock(Guid chainId)
    {
        throw new NotImplementedException();
    }

    public bool Store(Blockchain blockchain)
    {
        throw new NotImplementedException();
    }

    public bool Store(Guid chainId, Block block)
    {
        throw new NotImplementedException();
    }

    public bool Tail(Guid chainId, int n, out Block[] blocks)
    {
        throw new NotImplementedException();
    }
}