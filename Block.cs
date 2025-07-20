using System;
using System.Security.Cryptography;

namespace tax_registry_blockchain;

public class Block<PayloadT> where PayloadT : IRewarding, IAddressable
{
    public Block(PayloadT load)
    {
        TimeStamp = new DateTime().ToUniversalTime();
        PreviousHash = [0];
        Nonce = 0;
        Payload = load;
        Hash = Digest();
    }

    public Block()
    {
        TimeStamp = new DateTime().ToUniversalTime();
        PreviousHash = [0];
        Nonce = 0;
        Hash = Digest();
    }
    public DateTime TimeStamp { get; }
    public byte[] PreviousHash { get; set; }
    public byte[] Hash { get; set; }
    private int Nonce { get; set; }
    public PayloadT Payload { get; set; }

    public byte[] Digest()
    {
        return SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(ToString()));
    }

    /// <summary>
    /// Method 
    /// </summary>
    /// <param name="difficulty"></param>
    public void MineBlock(int difficulty)
    {
        while (!SatisfyCondition(difficulty))
        {
            ++Nonce;
            Hash = Digest();
        }
    }

    private bool SatisfyCondition(int difficulty)
    {
        int div = difficulty / 2;
        int residual = difficulty % 2;
        for (int i = 0; i < div; ++i)
        {
            if (Hash[i] != 0x0)
                return false;
        }
        if (residual != 0 && !IsLowerHalfByteNull(Hash[div]))
            return false;
        return true;
    }
    private static bool IsLowerHalfByteNull(byte b)
    {
        byte mask = 0x0F;
        return (b & mask) == 0x0;
    }

    public override string ToString()
    {
        return TimeStamp.ToString()
            + PreviousHash.ToString()
            + Nonce.ToString()
            + (Payload != null ? Payload.ToString() : "");
    }
}
