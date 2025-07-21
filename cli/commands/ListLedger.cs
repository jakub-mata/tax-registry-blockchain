using Spectre.Console;
using Spectre.Console.Cli;
using System;
using tax_registry_blockchain.storage;

namespace tax_registry_blockchain.cli;

public class ViewBlocksCommand : Command<ViewBlocksCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "blockchainName")]
        public string BlockchainID { get; set; }

        [CommandArgument(1, "count")]
        public int Count { get; set; } = 5;
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

        var blocks = storage.ListLedger(settings.BlockchainID);

        return 0;
    }

    private Spectre.Console.Table CreateTable(Block<TaxPayload>[] blocks)
    {
        var table = new Table();
        table.AddColumns([
            "From", "To", "Amount", "TaxType"
        ]);

        foreach (Block<TaxPayload> block in blocks)
        {
            foreach (TaxTransaction transaction in block.Payload.Transactions)
            {
                var enumString = (TaxTransaction.TType)transaction.TaxType;
                table.AddRow(
                    transaction.From, transaction.To, transaction.Amount.ToString(), enumString.ToString()
                );
            }
        }
        return table;
    }
}