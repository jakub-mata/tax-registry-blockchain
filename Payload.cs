using System;
using System.Collections.Generic;
using System.Linq;

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

public class TaxPayload
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

    public TaxPayload AddTransaction(string from, string to, decimal amount, string taxType)
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

    public override string ToString()
    {
        return Transactions.Aggregate("", (curr, next) => curr + next);
    }
}