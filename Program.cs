using System;

namespace tax_registry_blockchain;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
        Blockchain<TaxPayload> taxBlockchain = new(1);
        Console.WriteLine("Adding block 1...");
        taxBlockchain.AddBlock(new Block<TaxPayload>(
            1,
            TaxPayload.Create()
                .AddTransaction(
                    "Alice", "Bob", 100m, "Income"
                )
                .AddTransaction(
                    "Bob", "Charlie", 50m, "VAT"
                )
        ));
        Console.WriteLine("Adding block 2...");
        taxBlockchain.AddBlock(new Block<TaxPayload>(
            1,
            TaxPayload.Create()
                .AddTransaction(
                    "Bob", "Alice", 20m, "Returns"
                )
        ));
        taxBlockchain.Head();
    }
}
