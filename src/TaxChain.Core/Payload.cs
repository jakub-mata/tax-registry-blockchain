using System;
using System.ComponentModel;

namespace TaxChain.core;

public struct Transaction
{
    public Guid ID { get; set; }
    public string TaxpayerId { get; set; }          // Tax ID or SSN
    public decimal Amount { get; set; }             // Tax amount

    public Transaction(
        string taxpayerId,
        decimal amount
    )
    {
        ID = Guid.NewGuid();
        TaxpayerId = taxpayerId;
        Amount = amount;
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