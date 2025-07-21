using Spectre.Console;
using Spectre.Console.Cli;
using tax_registry_blockchain.storage;

namespace tax_registry_blockchain.cli.commands;

internal sealed class CreateCommand : Command<CreateCommand.Settings>
{
    public sealed class Settings : CommandSettings { }

    public override int Execute(CommandContext context, Settings settings)
    {
        AnsiConsole.MarkupLine("[bold blue]Blockchain creation wizard[/]");
        var storageType = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Where do you want to [bold blue]store the blockchain")
                .PageSize(10)
                .MoreChoicesText("[grey](Move up and down to enter the selection)[/]")
                .AddChoices([
                    "File",
                    "Database"
                ])
        );
        AnsiConsole.MarkupLine($"You've chosen [green]{storageType}");
        var blockchainName = AnsiConsole.Ask<string>("Enter the [green]name[/] of the blockchain:");
        SetUpStorage(blockchainName, storageType);
        
        AnsiConsole.MarkupLine($"[green]✔ Blockchain '{blockchainName}' created successfully");
        return 0;
    }

    private static void SetUpStorage(string blockchainName, string storageType)
    {
        // Create the request blockchain
        var blockchain = new Blockchain<TaxPayload>(
            name: blockchainName,
            difficulty: 4,
            reward: 10.0f,
            factory: new TaxPayloadFactory(),
            prf: MiningProof.ProofOfWork
        );
        IStorable storage = storageType switch
        {
            "File" => CLIClient.StorageManager.FileStore,
            "Database" => CLIClient.StorageManager.DBStore,
            _ => CLIClient.StorageManager.FileStore,
        };
        storage.Store(blockchain);
        CLIClient.StorageManager.blockchainToStorageMap.Add(blockchain.ID, storage);
    }
}

