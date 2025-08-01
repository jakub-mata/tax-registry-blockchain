using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;
using TaxChain.core;

namespace TaxChain.CLI.commands;

internal sealed class ListCommand : BaseAsyncCommand<ListCommand.Settings>
{
    public class Settings : CommandSettings { }
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        EnsureDaemonRunning();
        AnsiConsole.MarkupLine("Sending a list request to the chain deamon.[/]");
        try
        {
            Blockchain[]? fetched = await GetAllChains();
            if (fetched == null)
                return 1;
            if (fetched == null)
                {
                    AnsiConsole.MarkupLine("[red]Failed translate data from the daemon.[/]");
                    return 1;
                }
            string[] columns = { "chainId", "name", "rewardAmount", "difficulty" };
            string[,] rows = new string[fetched.Length, 4];
            for (int i = 0; i < fetched.Length; ++i)
            {
                rows[i, 0] = fetched[i].Id.ToString();
                rows[i, 1] = fetched[i].Name.ToString();
                rows[i, 2] = fetched[i].RewardAmount.ToString();
                rows[i, 3] = fetched[i].Difficulty.ToString();
            }
            AnsiConsole.Write(TableFactory.CreateTable(columns, rows));
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Failed to list blockchains.[/]");
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    internal static async Task<Blockchain[]?> GetAllChains()
    {
        AnsiConsole.MarkupLine("Sending a list request to the chain deamon.[/]");
        try
        {
            var response = await CLIClient.clientd.SendCommandAsync("list");
            if (!response.Success)
            {
                AnsiConsole.MarkupLine("Attempt to list blockchains by the daemon failed.[/]");
                AnsiConsole.MarkupLine($"Daemon's response: {response.Message}[/]");
                return null;
            }
            if (response.Data != null)
            {
                var blockchains = (Blockchain[])response.Data;
                return blockchains;
            }
            AnsiConsole.MarkupLine("[yellow]No data returned by the daemon.[/]");
            return null;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Failed to list blockchains.[/]");
            AnsiConsole.WriteException(ex);
            return null;
        }
    }
}
internal sealed class RemoveCommand : BaseAsyncCommand<RemoveCommand.Settings>
{
    public class Settings : CommandSettings
    { 
        [CommandOption("-c|--chain <CHAIN_ID>")]
        public string? ChainId { get; set; }
    }
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (settings.ChainId == null)
        {
            AnsiConsole.MarkupLine("[red]Chain Id has not been provided[/]");
            return 1;
        }

        EnsureDaemonRunning();
        AnsiConsole.MarkupLine("[grey]Calling daemon to remove the blockchain...[/]");
        try
        {
            bool ok = Guid.TryParse(settings.ChainId, out Guid parsed);
            if (!ok)
            {
                AnsiConsole.MarkupLine("[red]Failed to parse provided chain Id[/]");
                return 1;
            }
            var parameters = new Dictionary<string, Guid>()
            {
                {"chainId", parsed},
            };
            var response = await CLIClient.clientd.SendCommandAsync("remove");

            if (!response.Success)
            {
                AnsiConsole.Markup("[red]Attempt to list blockchains by the daemon failed.[/]");
                AnsiConsole.WriteLine($"Daemon's response: {response.Message}");
                return 1;
            }
            AnsiConsole.MarkupLine($"[green]Blockchain with id {settings.ChainId} removed successfully.[/]");
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
internal sealed class CreateCommand : BaseAsyncCommand<CreateCommand.Settings>
{
    public class Settings : CommandSettings {}
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var name = AnsiConsole.Prompt(
            new TextPrompt<string>("What's the name of the new taxchain?")
        );
        var reward = AnsiConsole.Prompt(
            new TextPrompt<float>("What's the reward amount for mining?")
        );
        var difficulty = AnsiConsole.Prompt(
            new TextPrompt<int>("What's the difficulty for proof-of-work (1-5 range recommended)?")
        );
        var blockchain = new Blockchain(
            name,
            reward,
            difficulty
        );
        var properties = new Dictionary<string, object>(){
            {"blockchain", blockchain},
        };
        AnsiConsole.WriteLine("Sending creation request to the daemon...");
        EnsureDaemonRunning();
        try
        {
            var response = await CLIClient.clientd.SendCommandAsync("create", properties);
            if (!response.Success)
            {
                AnsiConsole.Markup("[red]Attempt to create a new blockchain by the daemon failed.[/]");
                AnsiConsole.WriteLine($"Daemon's response: {response.Message}");
                return 1;
            }
            if (response.Data == null)
            {
                AnsiConsole.Markup("[yellow]Blockchain created but no id received. Should you wish to know its id, run the 'list' command.[/]");
                return 0;
            }
            Guid id = (Guid)response.Data;
            Console.Markup($"[green]Taxchain creation successful!. Here's its id: {id.ToString()}[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Failed to create a new blockchain.[/]");
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }
}
internal sealed class VerifyCommand : BaseAsyncCommand<VerifyCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-c|--chain <CHAIN_ID>")]
        public string? ChainId { get; set; }
    }
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (settings.ChainId == null)
        {
            AnsiConsole.MarkupLine("[yellow]No chain id provided, no chain to verify[/]");
            return 1;
        }
        bool ok = Guid.TryParse(settings.ChainId, out Guid parsed);
        if (!ok)
        {
            AnsiConsole.MarkupLine("[yellow]Failed to parse provided id. If unsure about the id, use the 'list' command[/]");
            return 1;
        }

        EnsureDaemonRunning();
        try
        {
            var parameters = new Dictionary<string, object>(){
                {"chainId", parsed},
            };
            var response = await CLIClient.clientd.SendCommandAsync("verify", parameters);
            if (!response.Success)
            {
                AnsiConsole.MarkupLine("[yellow]Failed to verify the chain.[/]");
                AnsiConsole.WriteLine($"Daemon message: {response.Message}");
                return 1;
            }
            AnsiConsole.MarkupLine($"[green]The taxchain {settings.ChainId} is valid.[/]");
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

internal sealed class FetchCommand : BaseAsyncCommand<FetchCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-c|--chain <CHAIN_ID>")]
        public string? ChainId { get; set; }
    }
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (settings.ChainId == null)
        {
            AnsiConsole.MarkupLine("[yellow]No chain id provided, no chain to verify[/]");
            return 1;
        }
        bool ok = Guid.TryParse(settings.ChainId, out Guid parsed);
        if (!ok)
        {
            AnsiConsole.MarkupLine("[yellow]Failed to parse provided id.[/]");
            return 1;
        }
        EnsureDaemonRunning();
        try
        {
            var properties = new Dictionary<string, object>()
            {
                {"chainId", parsed}
            };
            var response = await CLIClient.clientd.SendCommandAsync("fetch", properties);
            if (!response.Success)
            {
                AnsiConsole.MarkupLine("[red]Fetch unsuccessful.[/]");
                AnsiConsole.WriteLine($"Daemon's message: {response.Message}");
            }
            AnsiConsole.MarkupLine($"[green]Taxchain {settings.ChainId} fetched successfully.[/]");
            if (response.Data != null)
            {
                Blockchain b = (Blockchain)response.Data;
                AnsiConsole.MarkupLine($"[green]{b.Name} is now stored locally.[/]");
            }
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
internal sealed class SyncCommand : BaseAsyncCommand<SyncCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-c|--chain <CHAIN_ID>")]
        public string? ChainId { get; set; }
    }
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        EnsureDaemonRunning();
        int status = settings.ChainId == null
            ? await SyncAll()
            : await SyncOne(settings.ChainId);
        return status;
    }

    private async Task<int> SyncOne(string id)
    {
        bool ok = Guid.TryParse(id, out Guid parsed);
        if (!ok)
        {
            AnsiConsole.MarkupLine("[yellow]Failed to parse provided id.[/]");
            return 1;
        }
        try
        {
            var properties = new Dictionary<string, object>()
            {
                {"chainId", parsed},
            };
            var response = await CLIClient.clientd.SendCommandAsync("sync");
            if (!response.Success)
            {
                AnsiConsole.MarkupLine("[red]Synchorisation failed.[/]");
                AnsiConsole.WriteLine($"Daemon's message: ${response.Message}");
                return 1;
            }
            AnsiConsole.MarkupLine($"[green]Taxchain {id} has been synchronised![/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]An exception occured during verification.[/]");
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private async Task<int> SyncAll()
    {
        // get all ids, similar to 'list' command
        Blockchain[]? fetched = await ListCommand.GetAllChains();
        if (fetched == null)
            return 1;
        foreach (Blockchain b in fetched)
        {
            AnsiConsole.WriteLine($"Synchronising {b.Id.ToString()}...");
            int status = await SyncOne(b.Id.ToString());
            if (status == 1)
                AnsiConsole.MarkupLine($"Failed to sync blockchain with id [yellow]{b.Id.ToString()}[/]");
            else
                AnsiConsole.MarkupLine($"Successfully synced blockchain with id [green]{b.Id.ToString()}[/]");
        }
        AnsiConsole.WriteLine("Synchronisation has finished");
        return 0;
    }
}