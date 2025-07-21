using Spectre.Console.Cli;
using Spectre.Console;
using System;
using tax_registry_blockchain.storage;

namespace tax_registry_blockchain.cli;

internal sealed class AddBlockCommand : Command<AddBlockCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "blockchainName")]
        public string BlockchainID { get; set; }
    }
    public override int Execute(CommandContext context, Settings settings)
    {
        bool ok = CLIClient.StorageManager.blockchainToStorageMap.TryGetValue(
            Guid.Parse(settings.BlockchainID),
            out IStorable? storage
        );
        if (!ok || storage == null)
        {
            AnsiConsole.MarkupLine("[red]Blockchain not found.");
            return 1;
        }

        var from = AnsiConsole.Ask<string>("From address:");
        var to = AnsiConsole.Ask<string>("To address:");
        var amount = AnsiConsole.Ask<float>("Amount:");
        var type = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Transaction Type:")
                .AddChoices("Invoice", "NAT")
        );
        TaxTransaction.TType tt = type switch
        {
            "Invoice" => TaxTransaction.TType.Invoice,
            "NAT" => TaxTransaction.TType.NAT,
            _ => throw new NotImplementedException()
        };
        TaxPayload payload = TaxPayload
            .Create()
            .AddTransaction(
                from, to, amount, tt
            );

        storage.Store(new Block<TaxPayload>(payload));
        return 0;
    }
}