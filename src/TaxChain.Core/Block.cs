using System;
using System.Security.Cryptography;

namespace TaxChain.core;

public class Block
{
    public Block(string? prevHash, Transaction[] t)
    {
        PreviousHash = prevHash;
        Nonce = 0;
        Hash = Digest();
        Payload = t;
    }

    public Block(string prevHash, string hash, int nonce, Transaction[] payload)
    {
        PreviousHash = prevHash;
        Hash = hash;
        Nonce = nonce;
        Payload = payload;
    }
    public int Id { get; set; }
    public string? PreviousHash { get; set; }
    public string Hash { get; set; }
    public int Nonce { get; set; }
    public Transaction[] Payload { get; set; }

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
