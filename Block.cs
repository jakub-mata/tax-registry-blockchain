using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace tax_registry_blockchain;

public struct TaxTransaction
{
    public string From { get; set; }
    public string To { get; set; }
    public decimal Amount { get; set; }
    public string TaxType { get; set; }
    readonly public override string ToString()
    {
        return From + To + Amount.ToString() + TaxType;
    }
}

public abstract class PayloadType { }

public class TaxPayload : PayloadType
{
    public List<TaxTransaction> Transactions { get; set; }
    override public string ToString()
    {
        return Transactions.Aggregate("", (curr, next) => curr + next);
    }
}

public class Block
{
    public Block(int index, PayloadType load)
    {
        Index = index;
        TimeStamp = new DateTime().ToUniversalTime();
        PreviousHash = [0];
        Nonce = new Random().Next(1000);
        Payload = load;
        Hash = Digest();
    }

    public Block()
    {
        Index = 0;
        TimeStamp = new DateTime().ToUniversalTime();
        PreviousHash = [0];
        Nonce = new Random().Next(1000);
        Hash = Digest();
    }
    public int Index;
    public readonly DateTime TimeStamp;
    public byte[] PreviousHash;
    public byte[] Hash;
    public int Nonce;
    public PayloadType Payload;

    public byte[] Digest()
    {
        return SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(ToString()));
    }

    public override string ToString()
    {
        return Index.ToString()
            + TimeStamp
            + PreviousHash
            + Nonce
            + Payload.ToString();
    }
}

// BLOCK IMPLEMENTATIONS

public class TaxBlock : Block
{
    public TaxBlock(int index, TaxPayload taxPayload)
    : base(index, taxPayload) { }
}