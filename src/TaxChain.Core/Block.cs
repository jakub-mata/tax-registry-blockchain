using System;
using System.Security.Cryptography;
using System.Threading;

namespace TaxChain.core;

public class Block
{
    public Block(Guid chainId, string prevHash, Transaction t)
    {
        ChainId = chainId;
        PreviousHash = prevHash;
        Nonce = 0L;
        Payload = t;
        Timestamp = DateTime.Now;
        Hash = Digest();
    }

    public Block(Guid chainId, string prevHash, string hash, long nonce, DateTime timestamp, Transaction payload)
    {
        ChainId = chainId;
        PreviousHash = prevHash;
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

    public string Digest()
    {
        byte[] bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(ToString()));
        return Convert.ToHexString(bytes);
    }

    public void Mine(int difficulty, CancellationToken token)
    {
        var difficultyPrefix = new string('0', difficulty);
        Console.WriteLine($"Starting mining, looking for {difficultyPrefix} prefix");
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
        Console.WriteLine($"Found a valid nonce! {Nonce}");
    }

    public override string ToString()
    {
        return PreviousHash
            + Nonce.ToString()
            + Payload.ToString()
            + Timestamp.ToBinary().ToString();
    }
}
