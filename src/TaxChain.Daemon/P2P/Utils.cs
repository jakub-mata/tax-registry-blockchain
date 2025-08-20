using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.FileSystemGlobbing.Internal.PathSegments;
using TaxChain.core;

namespace TaxChain.Daemon.P2P;

public static class P2PUtils
{
    /// <summary>
    /// Validates blocks. Compares block's hash with digest, previous hash with next hash, and
    /// the amount of zero in a prefix based the chain's difficulty level.
    /// </summary>
    /// <param name="blocks">The blocks to be validated</param>
    /// <param name="difficulty">The amount of zero block's hashes have to start with</param>
    /// <returns>The validity of the blocks</returns>
    public static bool ValidateBlocks(List<Block> blocks, int difficulty)
    {
        if (blocks.Count == 0)
        {
            Console.WriteLine("Empty blocks");
            return false;
        }
        if (blocks.Count == 1)
            return true;

        string zeroPrefix = new string('0', difficulty);

        // Notice we're not validating the last (genesis) block
        for (int i = 0; i < blocks.Count - 1; ++i)
        {
            Block curr = blocks[i];
            Block next = blocks[i + 1];
            if (curr.Digest() != curr.Hash)
            {
                Console.WriteLine("Digest does not match current hash");
                return false;
            }
            if (!curr.Hash.StartsWith(zeroPrefix))
            {
                Console.WriteLine("Hash does not start with required prefix");
                return false;
            }
            if (curr.PreviousHash != next.Hash)
            {
                Console.WriteLine("Previous hash does not match hash");
                return false;
            }
        }
        return true;
    }
}
public record struct SyncStatus { public bool Success { get; set; } public DateTime DateTime { get; set; } };