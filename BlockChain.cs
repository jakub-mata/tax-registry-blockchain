using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace tax_registry_blockchain;

public abstract class Blockchain<T> where T : Block
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true
    };
    public List<Block> Chain { get; set; }
    public Blockchain()
    {
        Chain = [CreateGenesisBlock()];
    }

    /// <summary>
    /// Returns the last added block within the blockchain.
    /// </summary>
    /// <returns>The last block in the blockchain.</returns>
    public Block GetLatestBlock()
    {
        return Chain[^1];
    }

    /// <summary>
    /// Adds a new block to the blockchain. The method fetches the previous hash (which was 
    /// unknown upon creation of the passed block) and then has to recompute its hash.
    /// </summary>
    /// <param name="block">The block to be added to the blockchain.</param>
    public void AddBlock(T block)
    {
        block.PreviousHash = GetLatestBlock().Hash;
        block.Hash = block.Digest();
        Chain.Add(new Block());
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
    protected Block CreateGenesisBlock()
    {
        return new Block();
    }
}

public class TaxRegistryBlockchain : Blockchain<TaxBlock>
{
    
}