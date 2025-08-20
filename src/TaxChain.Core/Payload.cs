using System;
using System.ComponentModel;
using System.Linq;

namespace TaxChain.core;

/// <summary>
/// Represents a transaction, each relating to a block in the blockchain.
/// Each transaction has a unique ID, a taxpayer ID (or SSN), and an amount.
/// </summary>
public struct Transaction
{
    public Guid ID { get; set; }
    public string TaxpayerId { get; set; }          // Tax ID or SSN
    public float Amount { get; set; }             // Tax amount
    public TaxType Type { get; set; }               // Type of tax, see TaxType
    public readonly string GetTaxType()
    {
        var field = typeof(TaxType).GetField(Type.ToString());
        DescriptionAttribute? attr = (DescriptionAttribute?)Attribute.GetCustomAttribute(field!, typeof(DescriptionAttribute));
        return attr?.Description ?? Type.ToString();
    }

    public static string ConvertTaxType(TaxType taxType)
    {
        var field = typeof(TaxType).GetField(taxType.ToString());
        DescriptionAttribute? attr = (DescriptionAttribute?)Attribute.GetCustomAttribute(field!, typeof(DescriptionAttribute));
        return attr?.Description ?? taxType.ToString();
    }

    public static TaxType GetTaxTypeFromString(string description)
    {
        foreach (TaxType type in Enum.GetValues(typeof(TaxType)))
        {
            var field = typeof(TaxType).GetField(type.ToString());
            var attr = (DescriptionAttribute?)Attribute.GetCustomAttribute(field!, typeof(DescriptionAttribute));
            if ((attr?.Description ?? type.ToString()).Equals(description, StringComparison.OrdinalIgnoreCase))
            {
                return type;
            }
        }
        throw new ArgumentException($"No TaxType with description '{description}' found.");
    }

    public static TaxType[] GetTaxTypeValues()
    {
        return Enum.GetValues(typeof(TaxType)).Cast<TaxType>().ToArray();
    }

    public Transaction(
        string taxpayerId,
        float amount,
        TaxType type
    )
    {
        ID = Guid.NewGuid();
        TaxpayerId = taxpayerId;
        Amount = amount;
        Type = type;
    }
    public Transaction(
        Guid id,
        string taxpayerId,
        float amount,
        TaxType type
    )
    {
        ID = id;
        TaxpayerId = taxpayerId;
        Amount = amount;
        Type = type;
    }

    public override string ToString()
    {
        return $"{ID}+{TaxpayerId}+{Amount}";
    }

    public void Print()
    {
        Console.WriteLine(ToString());
    }
}
public enum TaxType
{
    [Description("income")]
    Income,
    [Description("dividence")]
    Dividence,
    [Description("property")]
    Property,
    [Description("consumption")]
    Consumption,
    [Description("tariff")]
    Tariff,
    [Description("reward")]
    Reward
}