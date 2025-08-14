using System;
using System.Security.Cryptography;

namespace TaxChain.core;

public class Block
{
    public Block(Guid chainId, string prevHash, Transaction t)
    {
        ChainId = chainId;
        PreviousHash = prevHash;
        Nonce = 0L;
        Hash = Digest();
        Payload = t;
        Timestamp = DateTime.Now;
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
        string? hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(ToString())).ToString();
        return hash ?? "";
    }

    public override string ToString()
    {
        return PreviousHash ?? ""
            + Nonce.ToString()
            + Payload.ToString();
    }
}
