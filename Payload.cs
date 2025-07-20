using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Transactions;

namespace tax_registry_blockchain;

public struct TaxTransaction
{
    public string From { get; set; }
    public string To { get; set; }
    public float Amount { get; set; }
    public TType TaxType { get; set; }
    readonly public override string ToString()
    {
        return From + To + Amount.ToString() + TaxType;
    }
    public enum TType
    {
        Invoice,
        NAT,
        Reward,
    }
}

public interface IRewarding
{
    public void MakeAReward(string from, string to, float reward);
}

public interface IAddressable
{
    public float GetTotalByAddress(string address);
}

public class TaxPayload : IRewarding, IAddressable
{
    public List<TaxTransaction> Transactions { get; set; }

    private TaxPayload()
    {
        Transactions = new List<TaxTransaction>();
    }

    public static TaxPayload Create()
    {
        return new TaxPayload();
    }

    public TaxPayload AddTransaction(string from, string to, float amount, TaxTransaction.TType taxType)
    {
        Transactions.Add(new TaxTransaction
        {
            From = from,
            To = to,
            Amount = amount,
            TaxType = taxType
        });
        return this;
    }

    public TaxPayload AddTransaction(TaxTransaction transaction)
    {
        Transactions.Add(transaction);
        return this;
    }

    public void MakeAReward(string from, string to, float reward)
    {
        Transactions.Clear();
        Transactions.Add(new TaxTransaction
        {
            From = from,
            To = to,
            Amount = reward,
            TaxType = TaxTransaction.TType.Reward,
        });
    }

    public float GetTotalByAddress(string address)
    {
        return Transactions.Aggregate(0f, (curr, nextTrans) =>
        {
            if (nextTrans.From == address)
                curr -= nextTrans.Amount;
            if (nextTrans.To == address)
                curr += nextTrans.Amount;
            return curr;
        });      
    }

    public override string ToString()
    {
        return Transactions.Aggregate("", (curr, next) => curr + next);
    }
}

public interface IPayloadFactory<T> where T : IRewarding
{
    T CreateReward(string from, string to, float reward);
}

public class TaxPayloadFactory : IPayloadFactory<TaxPayload>
{
    public TaxPayload CreateReward(string from, string to, float reward)
    {
        var payload = TaxPayload.Create();
        payload.MakeAReward(from, to, reward);
        return payload;
    }
}