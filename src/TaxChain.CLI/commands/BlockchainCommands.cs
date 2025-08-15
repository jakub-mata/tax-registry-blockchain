using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;
using TaxChain.core;

namespace TaxChain.CLI.commands;

public class BlockchainSettings : CommandSettings
{
    [CommandArgument(0, "<BLOCKCHAIN_ID>")]
    public string BlockchainId { get; set; } = string.Empty;
    [CommandOption("--verbose")]
    [DefaultValue(false)]
    public bool Verbose { get; set; }
}

internal sealed class AddBlockCommand : BaseAsyncCommand<AddBlockCommand.Settings>
{
    public class Settings : BlockchainSettings { }
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        bool ok = Guid.TryParse(settings.BlockchainId, out Guid parsed);
        if (!ok)
        {
            AnsiConsole.MarkupLine("[red]Failed to parse provided chain id. If unsure, run the 'list' command.[/]");
            return 1;
        }
        string taxpayerId = AnsiConsole.Prompt(
            new TextPrompt<string>("Type your taxpayer id:")
        );
        decimal amount = AnsiConsole.Prompt<decimal>(
            new TextPrompt<decimal>("Write the amount:")
        );
        var transaction = new Transaction(
            taxpayerId,
            amount
        );
        return await SendAddRequest(transaction, parsed, settings.Verbose);
    }

    private async Task<int> SendAddRequest(core.Transaction t, Guid chainId, bool verbose = false)
    {
        await EnsureDaemonRunning();
        var properties = new Dictionary<string, object>()
        {
            {"chainId", chainId},
            { "transaction", t},
            {"verbose", verbose}
        };
        AnsiConsole.WriteLine("Sending provided transaction to the records...");
        try
        {
            var response = await CLIClient.clientd.SendCommandAsync("add", properties);
            if (!response.Success)
            {
                AnsiConsole.MarkupLine("[red]Failed to add block to taxchain.[/]");
                AnsiConsole.WriteLine($"Daemon's message: {response.Message}");
                return 1;
            }
            AnsiConsole.MarkupLine($"[green]Successfully added a block to chain {chainId}![/]");
            AnsiConsole.MarkupLine($"[yellow]If you want to ensure it gets appended, run the 'mine' command.[/]");
            return 0; 
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Exception occured while adding a new block.[/]");
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }
}
internal sealed class GatherCommand : BaseAsyncCommand<GatherCommand.Settings>
{
    public class Settings : BlockchainSettings
    {
        [CommandOption("-u|--user <USER_ADDRESS>")]
        public string? UserAddress { get; set; }
    }
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var properties = new Dictionary<string, object>();
        if (settings.UserAddress == null || settings.BlockchainId == null)
        {
            AnsiConsole.MarkupLine("[red]Failed to provide necessary options.[/]");
            return 1;
        }
        properties.Add("taxpayerId", settings.UserAddress);
        properties.Add("chainId", Guid.Parse(settings.BlockchainId));
        properties.Add("verbose", settings.Verbose);

        await EnsureDaemonRunning();
        AnsiConsole.WriteLine("Sending a request for the gather command...");
        try
        {
            var response = await CLIClient.clientd.SendCommandAsync("gather", properties);
            if (!response.Success)
            {
                AnsiConsole.MarkupLine("[red]Failed to gather taxpayer information. Try again later.[/]");
                AnsiConsole.WriteLine($"Daemon's message: {response.Message}");
                return 1;
            }
            if (response.Data == null)
            {
                AnsiConsole.MarkupLine("[red]Daemon did not send any data. Try again later.[/]");
                return 1;
            }
            List<Transaction>? info = (response.Data is JsonElement jsonElement)
                ? JsonSerializer.Deserialize<List<Transaction>>(jsonElement)
                : (List<Transaction>)response.Data;
            if (info == null)
            {
                AnsiConsole.MarkupLine("[red]Could not parse received data.[/]");
                return 1;
            }

            DisplayTaxpayerTable(info);
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Exception occured while gathering taxpayer data.[/]");
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private static void DisplayTaxpayerTable(List<Transaction> data)
    {
        if (data.Count == 0)
            AnsiConsole.WriteLine("No known entries for this user.");
        foreach (Transaction t in data)
        {
            AnsiConsole.MarkupLine($"ID: [green]{t.TaxpayerId}[/]");
            AnsiConsole.MarkupLine($"Amount: [green]{t.Amount}[/]");
            AnsiConsole.WriteLine();
        }
    }
}
internal sealed class LedgerCommand : BaseAsyncCommand<LedgerCommand.Settings>
{
    private readonly int _defaultNumber = 5;
    public class Settings : BlockchainSettings
    {
        [CommandOption("-n|--number <AMOUNT>")]
        public int? Number { get; set; }
    }
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        settings.Number ??= _defaultNumber;
        var parameters = new Dictionary<string, object>()
        {
            {"number", settings.Number},
            {"chainId", Guid.Parse(settings.BlockchainId)},
            {"verbose", settings.Verbose}
        };
        await EnsureDaemonRunning();
        AnsiConsole.WriteLine($"Sending a request for a ledger of size {settings.Number}");
        try
        {
            var response = await CLIClient.clientd.SendCommandAsync("ledger", parameters);
            if (!response.Success)
            {
                AnsiConsole.MarkupLine("[red]Failed to fetch ledger. Try again later.[/]");
                AnsiConsole.WriteLine($"Daemon's message: {response.Message}");
                return 1;
            }
            if (response.Data == null)
            {
                AnsiConsole.MarkupLine("[red]Daemon did not send any data. Try again later.[/]");
                return 1;
            }
            List<Block>? blocks = (response.Data is JsonElement jsonElement)
                ? JsonSerializer.Deserialize<List<Block>>(jsonElement.GetRawText())
                : (List<Block>)response.Data;
            if (blocks == null)
            {
                AnsiConsole.MarkupLine("[red]Unable to parse received data.[/]");
                return 1;
            }
            AnsiConsole.Write(TableFactory.CreateLedgerTable(blocks));
            AnsiConsole.WriteLine();
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Exception occured while fetching the ledger.[/]");
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }
}

internal sealed class RemoveCommand : BaseAsyncCommand<RemoveCommand.Settings>
{
    public class Settings : BlockchainSettings { }
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (settings.BlockchainId == "")
        {
            AnsiConsole.MarkupLine("[red]Chain Id has not been provided[/]");
            return 1;
        }

        await EnsureDaemonRunning();
        AnsiConsole.MarkupLine("[grey]Calling daemon to remove the blockchain...[/]");
        try
        {
            bool ok = Guid.TryParse(settings.BlockchainId, out Guid parsed);
            if (!ok)
            {
                AnsiConsole.MarkupLine("[red]Failed to parse provided chain Id[/]");
                return 1;
            }
            var parameters = new Dictionary<string, object>()
            {
                {"chainId", parsed},
                {"verbose", settings.Verbose}
            };
            var response = await CLIClient.clientd.SendCommandAsync("remove", parameters);

            if (!response.Success)
            {
                AnsiConsole.MarkupLine("[red]Attempt to remove a blockchain by the daemon failed.[/]");
                AnsiConsole.WriteLine($"Daemon's response: {response.Message}");
                return 1;
            }
            AnsiConsole.MarkupLine($"[green]Blockchain with id {settings.BlockchainId} removed successfully.[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Failed to remove blockchain.[/]");
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }
}

internal sealed class MineCommand : BaseAsyncCommand<MineCommand.Settings>
{
    public class Settings : BlockchainSettings { }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (settings.BlockchainId == "")
        {
            AnsiConsole.MarkupLine("[red]Chain Id has not been provided[/]");
            return 1;
        }
        await EnsureDaemonRunning();
        AnsiConsole.MarkupLine("[grey]Calling the daemon to start mining...[/]");
        try
        {
            bool ok = Guid.TryParse(settings.BlockchainId, out Guid chainId);
            if (!ok)
            {
                AnsiConsole.MarkupLine("[red]Failed to parse provided chain Id[/]");
                return 1;
            }
            var parameters = new Dictionary<string, object>()
            {
                {"chainId", chainId},
                {"verbose", settings.Verbose}
            };
            var response = await CLIClient.clientd.SendCommandAsync("mine", parameters);
            if (!response.Success)
            {
                AnsiConsole.MarkupLine("[yellow]Mining has not started...[/]");
                AnsiConsole.WriteLine($"Daemon's response: {response.Message}");
                return 1;
            }
            if (response.Message == "No pending transactions")
            {
                AnsiConsole.MarkupLine("[green]Nothing to mine...[/]");
                return 0;
            }
            AnsiConsole.MarkupLine($"[green]Mining of blockchain {chainId.ToString()} has started![/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Failed to start mining.[/]");
            AnsiConsole.WriteException(ex);
            return 1;
        }

    }
}

internal sealed class InfoCommand : BaseAsyncCommand<InfoCommand.Settings>
{
    public class Settings : BlockchainSettings { }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (settings.BlockchainId == "")
        {
            AnsiConsole.MarkupLine("[red]Chain Id has not been provided[/]");
            return 1;
        }
        await EnsureDaemonRunning();
        AnsiConsole.MarkupLine("[grey]Calling the daemon to get info...[/]");
        try
        {
            bool ok = Guid.TryParse(settings.BlockchainId, out Guid chainId);
            if (!ok)
            {
                AnsiConsole.MarkupLine("[red]Failed to parse provided chain Id[/]");
                return 1;
            }
            var parameters = new Dictionary<string, object>()
            {
                {"chainId", chainId},
                {"verbose", settings.Verbose}
            };
            var response = await CLIClient.clientd.SendCommandAsync("info", parameters);
            if (!response.Success)
            {
                AnsiConsole.MarkupLine("[yellow]Daemon failed...[/]");
                AnsiConsole.WriteLine($"Daemon's response: {response.Message}");
                return 1;
            }
            Blockchain? b = (response.Data is JsonElement jsonElement)
                ? JsonSerializer.Deserialize<Blockchain>(jsonElement)
                : (Blockchain?)response.Data;

            if (b == null)
            {
                AnsiConsole.MarkupLine("[red]Failed to parsed received blockchain.[/]");
                return 1;
            }
            AnsiConsole.MarkupLine($"ChainID: [green]{b.Value.Id.ToString()}[/]");
            AnsiConsole.MarkupLine($"Reward: [green]{b.Value.RewardAmount}[/]");
            AnsiConsole.MarkupLine($"Difficulty: [green]{b.Value.Difficulty}[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Failed to fetch info.[/]");
            AnsiConsole.WriteException(ex);
            return 1;
        }

    }
}

internal sealed class VerifyCommand : BaseAsyncCommand<VerifyCommand.Settings>
{
    public class Settings : BlockchainSettings {}
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (settings.BlockchainId == null)
        {
            AnsiConsole.MarkupLine("[yellow]No chain id provided, no chain to verify[/]");
            return 1;
        }
        bool ok = Guid.TryParse(settings.BlockchainId, out Guid parsed);
        if (!ok)
        {
            AnsiConsole.MarkupLine("[yellow]Failed to parse provided id. If unsure about the id, use the 'list' command[/]");
            return 1;
        }

        await EnsureDaemonRunning();
        try
        {
            var parameters = new Dictionary<string, object>(){
                {"chainId", parsed},
                {"verbose", settings.Verbose}
            };
            var response = await CLIClient.clientd.SendCommandAsync("verify", parameters);
            if (!response.Success)
            {
                AnsiConsole.MarkupLine("[yellow]Failed to verify the chain.[/]");
                AnsiConsole.WriteLine($"Daemon message: {response.Message}");
                return 1;
            }
            AnsiConsole.MarkupLine($"[green]The taxchain {settings.BlockchainId} is valid.[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]An exception occured during verification.[/]");
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }
}