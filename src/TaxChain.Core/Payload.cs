using System;
using System.ComponentModel;

namespace TaxChain.core;

public struct Transaction
{
    public Guid ID;
    public string TaxpayerId { get; set; }          // Tax ID or SSN
    public decimal Amount { get; set; }             // Tax amount

    public static Transaction Build()
    {
        Transaction t = new Transaction();
        t.ID = new Guid();
        return t;
    }
    public Transaction AddAmount(decimal amount)
    {
        Amount = amount;
        return this;
    }
    public Transaction AddTaxpayerId(string payerId)
    {
        TaxpayerId = payerId;
        return this;
    }
    public Transaction(
        Guid id,
        string taxpayerId,
        decimal amount
    )
    {
        ID = id;
        TaxpayerId = taxpayerId;
        Amount = amount;
    }

    public override string ToString()
    {
        return $"{ID}{TaxpayerId}{Amount}";
    }
}