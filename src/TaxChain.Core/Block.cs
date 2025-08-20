using System;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading;

namespace TaxChain.core;

/// <summary>
/// Represents a block in the blockchain.
/// Each block contains a reference to the previous block, a hash of its contents,
/// a nonce for mining, a timestamp, and a payload which is typically a transaction.
/// The block's hash is computed based on its contents, ensuring integrity and immutability.
/// The block can be mined by finding a nonce that produces a hash with a specific prefix.
/// </summary>
public class Block
{
    public Block(Guid chainId, string prevHash, Transaction t)
    {
        ChainId = chainId;
        PreviousHash = prevHash;
        Nonce = 0L;
        Payload = t;
        Timestamp = DateTime.UtcNow;
        Hash = Digest();
    }

    [JsonConstructor]
    public Block(Guid chainId, string previousHash, string hash, long nonce, DateTime timestamp, Transaction payload)
    {
        ChainId = chainId;
        PreviousHash = previousHash;
        Hash = hash;
        Nonce = nonce;
        Timestamp = timestamp;
        Payload = payload;
    }
    public Guid ChainId { get; set; }
    public string PreviousHash { get; set; }
    public string Hash { get; set; }
    public long Nonce { get; set; }
    public DateTime Timestamp { get; set; }
    public Transaction Payload { get; set; }

    /// <summary>
    /// Computes the SHA-256 hash of the block's contents.
    /// The hash is a unique identifier for the block, ensuring its integrity.
    /// </summary>
    /// <returns>A hexadecimal string representing the hash of the block.</returns>
    public string Digest()
    {
        byte[] bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(ToString()));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Mines the block by finding a nonce that produces a hash with a specific prefix.
    /// </summary>
    /// <param name="difficulty">The amount of zero prefexing the hash</param>
    /// <param name="token">Cancelation token in case the mining should be stopped</param>
    /// <exception cref="OverflowException"></exception>
    public void Mine(int difficulty, CancellationToken token)
    {
        var difficultyPrefix = new string('0', difficulty);
        Nonce = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            Hash = Digest();
            if (Hash.StartsWith(difficultyPrefix))
                break;
            if (Nonce == long.MaxValue)
                throw new OverflowException("Mining not possible, all possible nonce values tried");
            Nonce++;
        }
    }

    public override string ToString()
    {
        return $"{PreviousHash}-{Nonce.ToString()}-{Payload.ToString()}-{Timestamp.Ticks.ToString()}";
    }

    public void Print()
    {
        Console.WriteLine("-----");
        Console.WriteLine($"Prev: ${PreviousHash}");
        Console.WriteLine($"Hash: ${Hash}");
        Payload.Print();
    }
}
