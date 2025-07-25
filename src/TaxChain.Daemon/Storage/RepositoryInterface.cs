using System;
using TaxChain.core;

namespace TaxChain.Daemon.Storage;

public interface IBlockchainRepository
{
    /// <summary>
    /// Stores the given blockchain into local storage. Note that the genesis block should
    /// be stored as well, which this method is not responsible for.
    /// </summary>
    /// <param name="blockchain">The blockchain to be stored.</param>
    /// <returns>Bool: the success of the operation.</returns>
    public bool Store(Blockchain blockchain);
    /// <summary>
    /// Appends the given block to the specified blockchain in local storage.
    /// </summary>
    /// <param name="chainId">The blockchain which is to be appended.</param>
    /// <param name="block">The block to be appended.</param>
    /// <returns>Bool: the success of the operation.</returns>
    public bool Store(Guid chainId, Block block);
    /// <summary>
    /// Adds a transaction into pending transaction of the given blockchain. These
    /// transactions are not a part of the blockchain. Only when mining starts can
    /// these transactions be retrieved and added to the blockchain.
    /// </summary>
    /// <param name="transaction">The transaction to be equeued.</param>
    /// <returns>Bool: the success of the operation.</returns>
    public bool EnqueueTransaction(Transaction transaction);
    /// <summary>
    /// Removes the last block of a blockchain.
    /// </summary>
    /// <param name="chainId">The blockchain whose last block is to be removed.</param>
    /// <returns>Bool: the success of the operation.</returns>
    public bool RemoveLastBlock(Guid chainId);
    /// <summary>
    /// Removes the blockchain representation from the local storage.
    /// This includes all the blocks and pending transactions related to it.
    /// </summary>
    /// <param name="chainId">The blockchain to be removed.</param>
    /// <returns>Bool: the success of the operation.</returns>
    public bool RemoveChain(Guid chainId);
    /// <summary>
    /// Fetches any of the pending operations of the given blockchain.
    /// </summary>
    /// <param name="chainId">The blockchain whose pending operations are to be searched.</param>
    /// <param name="transaction">The returned transaction. If no transactions are pending,
    /// the returned value is null</param>
    /// <returns></returns>
    public bool FetchPending(Guid chainId, out Transaction? transaction);
    /// <summary>
    /// Fetches n last blocks from a blockchain.
    /// </summary>
    /// <param name="chainId">Blockchain whose blocks are to be fetched</param>
    /// <param name="n">The amount of blocks to be fetched</param>
    /// <param name="blocks">The fetched blocks</param>
    /// <returns>Bool: the success of the operation.</returns>
    public bool Tail(Guid chainId, int n, out Block[] blocks);
    /// <summary>
    /// Fetches all the blocks in the blockchain, in descending order.
    /// </summary>
    /// <param name="chainId">Blockchain whose blocks are to be fetched</param>
    /// <param name="blocks">The fetched blocks in descending order</param>
    /// <returns>Bool: the success of the operation.</returns>
    public bool Fetch(Guid chainId, out Block[] blocks);
}