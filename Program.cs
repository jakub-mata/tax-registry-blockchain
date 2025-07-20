using System;

namespace tax_registry_blockchain;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
        Blockchain<TaxPayload> taxBlockchain = new(
            1,
            100,
            new TaxPayloadFactory(),
            MiningProof.ProofOfWork
        );
        Console.WriteLine("Adding block 1...");
        taxBlockchain.AddBlock(new Block<TaxPayload>(
            TaxPayload.Create()
                .AddTransaction(
                    "Alice", "Bob", 100f, TaxTransaction.TType.Invoice
                )
                .AddTransaction(
                    "Bob", "Charlie", 50f, TaxTransaction.TType.NAT
                )
        ));
        Console.WriteLine("Adding block 2...");
        taxBlockchain.AddBlock(new Block<TaxPayload>(
            TaxPayload.Create()
                .AddTransaction(
                    "Bob", "Alice", 20f, TaxTransaction.TType.Invoice
                )
        ));
        taxBlockchain.Head();
    }
}
