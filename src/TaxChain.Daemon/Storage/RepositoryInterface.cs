using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using TaxChain.core;

namespace TaxChain.Daemon.Storage;

public interface IBlockchainRepository
{
    public void Initialize();
    /// <summary>
    /// Stores the given blockchain into local storage. Note that the genesis block is
    /// created and stored as well.
    /// </summary>
    /// <param name="blockchain">The blockchain to be stored.</param>
    /// <returns>Bool: the success of the operation.</returns>
    public bool Store(Blockchain blockchain);
    /// <summary>
    /// Appends a new block to the given blockchain. Verifies the validity of the block
    /// by comparing its hash and digest + prev_hash with last block.
    /// </summary>
    /// <param name="block">The block to be appended.</param>
    /// <returns>AppendResult: the result of the operation, see AppendResult</returns>
    public AppendResult AppendBlock(Block block, bool isReward = false);
    /// <summary>
    /// Retrieves the representation of a blockchain with given id from local storage.
    /// </summary>
    /// <param name="chainId">The ID of the blockchain.</param>
    /// <param name="isReward">Is the block to be appended a reward for mining.</param>
    /// <returns>Bool: the success of the operation</returns>
    public bool GetBlockchain(Guid chainId, out Blockchain? b);
    /// <summary>
    /// Adds a transaction into pending transaction of the given blockchain. These
    /// transactions are not a part of the blockchain. Only when mining starts can
    /// these transactions be retrieved and added to the blockchain.
    /// </summary>
    /// <param name="transaction">The transaction to be equeued.</param>
    /// <returns>Bool: the success of the operation.</returns>
    public bool EnqueueTransaction(Guid chainId, Transaction transaction);
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
    public bool Tail(Guid chainId, int n, out List<Block> blocks);
    /// <summary>
    /// Fetches all the blocks in the blockchain, in descending order.
    /// </summary>
    /// <param name="chainId">Blockchain whose blocks are to be fetched</param>
    /// <param name="blocks">The fetched blocks in descending order</param>
    /// <returns>Bool: the success of the operation.</returns>
    public bool Fetch(Guid chainId, out List<Block> blocks);
    /// <summary>
    /// Fetches all blockchains stored in the local database.
    /// </summary>
    /// <returns>Bool: the success of the operation.</returns>
    public bool ListChains(out List<Blockchain> chains);
    /// <summary>
    /// Verifies the validity of a blockchain defined by id provided.
    /// </summary>
    /// <param name="chainId">The id of the blockchain to verity</param>
    /// <returns>Bool: the validity of the blockchain.</returns>
    public bool Verify(Guid chainId);
    /// <summary>
    /// Gathers the balance of a taxpayer in a blockchain.
    /// </summary>
    /// <param name="chainId">The id of the blockchain</param>
    /// <param name="taxpayerId">Taxpayer's id</param>
    /// <returns>Bool: the success of the operation</returns>
    public bool GatherTaxpayer(Guid chainId, string taxpayerId, out List<Transaction> transactions);
    /// <summary>
    /// Replace all blocks and their respective transactions (keeping pending
    /// transactions) within a given chain with blocks provided. Validation
    /// not included and has to be done before calling.
    /// </summary>
    /// <param name="chainId">The ID of the chain</param>
    /// <param name="blocks">The replacement blocks in reverse order (newest to oldest)</param>
    /// <returns>Bool: the success of the operation</returns>
    public bool ReplaceChainBlocks(Guid chainId, List<Block> blocks);
    /// <summary>
    /// Counts the length of the blockchain (the amount of blocks).
    /// </summary>
    /// <param name="chainId">The ID of the blockchain</param>
    /// <param name="count">The amount of blocks within the blockchain</param>
    /// <returns>Bool: the success of the operation</returns>
    public bool CountBlocks(Guid chainId, out int count);
}

public enum AppendResult
{
    Success,
    BlockchainUndefined,
    PrevHashMismatch,
    DigestMismatch,
    AlreadyIn,
    DBFail,
    Exception,
}