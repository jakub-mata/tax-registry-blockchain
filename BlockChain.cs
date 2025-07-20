using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Transactions;

namespace tax_registry_blockchain;

public enum MiningProof
{
    ProofOfWork,
}
public class Blockchain<T> where T : IRewarding, IAddressable
{
    public int Difficulty { get; }
    private readonly IPayloadFactory<T> payloadFactory;
    private readonly MiningProof proofType;
    private List<T> pendingTransactions;
    public float Reward { get; init; }
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true
    };
    public List<Block<T>> Chain { get; set; }
    public Blockchain(int difficulty, float reward, IPayloadFactory<T> factory, MiningProof prf)
    {
        Chain = [CreateGenesisBlock()];
        proofType = prf;
        Difficulty = difficulty;
        pendingTransactions = [];
        Reward = reward;
        payloadFactory = factory;
    }

    /// <summary>
    /// Returns the last added block within the blockchain.
    /// </summary>
    /// <returns>The last block in the blockchain.</returns>
    public Block<T> GetLatestBlock()
    {
        return Chain[^1];
    }

    public float GetBalanceOfAddress(string address)
    {
        return Chain.Aggregate(0f, (curr, nextBlock) =>
        {
            if (nextBlock.Payload == null)
                return curr;
            return curr + nextBlock.Payload.GetTotalByAddress(address);
        }
        );
    }

    /// <summary>
    /// Adds a new block to the blockchain. The method fetches the previous hash (which was 
    /// unknown upon creation of the passed block) and then has to recompute its hash.
    /// </summary>
    /// <param name="block">The block to be added to the blockchain.</param>
    private void AddBlock(Block<T> block)
    {
        block.PreviousHash = GetLatestBlock().Hash;
        Console.WriteLine("Mining starts...");
        block.MineBlock(Difficulty);
        Chain.Add(block);
    }

    public void AddTransaction(T transaction)
    {
        pendingTransactions.Add(transaction);
    }

    public void MinePendingTransactions(int blockToMineIndex, string rewardAddress)
    {
        Block<T> block = new(pendingTransactions[blockToMineIndex]);
        AddBlock(block);
        pendingTransactions.RemoveAt(blockToMineIndex);
        Console.WriteLine("Block mined successfully. Sending mining reward...");
        SendMiningReward(null, rewardAddress);
        Console.WriteLine("Mining reward added to pending successfully.");
    }

    private void SendMiningReward(string from, string to)
    {
        T payload = payloadFactory.CreateReward(from, to, Reward);
        pendingTransactions.Add(payload);
    }

    /// <summary>
    /// Prints out the blocks contained within the blockchain in a JSON format.
    /// Blocks are printed out from the back (tail-end).
    /// </summary>
    /// <param name="amount">The amount of blocks to print out.</param>
    public void Head(int amount = 5)
    {
        var blocksToPrint = Chain.TakeLast(amount);
        string json = JsonSerializer.Serialize(
            blocksToPrint, jsonOptions
        );
        Console.WriteLine(json);
    }

    /// <summary>
    /// Validation of the blockchain. Validation is done by checking whether a block's hash
    /// correctly represents the its content and whether its previousHash value equals the 
    /// Hash value of the previous block.
    /// </summary>
    /// <returns>True if valid (not tampered with), false if invalid (tampered with)</returns>
    public bool IsValid()
    {
        for (int i = 1; i < Chain.Count; i++)
        {
            // Check if the current hash is represents its content
            if (Chain[i].Hash != Chain[i].Digest())
                return false;
            // Check if previous hash points to the correct block
            if (Chain[i].PreviousHash != Chain[i - 1].Hash)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Creates the first ('genesis') block to start off the blockhain.
    /// The genesis block can contain random information. This method is called in the
    /// constructor.
    /// </summary>
    /// <returns>The genesis block</returns>
    protected Block<T> CreateGenesisBlock()
    {
        return new Block<T>();
    }
}