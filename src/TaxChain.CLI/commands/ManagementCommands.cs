using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;
using TaxChain.core;

namespace TaxChain.CLI.commands;

internal sealed class ListCommand : BaseAsyncCommand<ListCommand.Settings>
{
    public class Settings : VerboseSettings { }
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        await EnsureDaemonRunning();
        AnsiConsole.MarkupLine("Sending a list request to the chain deamon.");
        try
        {
            Blockchain[]? fetched = await GetAllChains(GetParameters(settings.Verbose));
            if (fetched == null)
                return 1;
            if (fetched == null)
                {
                    AnsiConsole.MarkupLine("[red]Failed translate data from the daemon.[/]");
                    return 1;
                }
            string[] columns = { "chainId", "name", "rewardAmount", "difficulty" };
            string[][] rows = new string[fetched.Length][];
            for (int i = 0; i < fetched.Length; ++i)
            {
                rows[i] = [
                    fetched[i].Id.ToString(),
                    fetched[i].Name.ToString(),
                    fetched[i].RewardAmount.ToString(),
                    fetched[i].Difficulty.ToString()
                ];
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

    internal static async Task<Blockchain[]?> GetAllChains(Dictionary<string, object>? param = null)
    {
        AnsiConsole.MarkupLine("Sending a list request to the chain deamon.");
        try
        {
            var response = await CLIClient.clientd.SendCommandAsync("list", param);
            if (!response.Success)
            {
                AnsiConsole.MarkupLine("Attempt to list blockchains by the daemon failed.");
                AnsiConsole.MarkupLine($"Daemon's response: {response.Message}");
                return null;
            }
            if (response.Data != null)
            {
                var blockchains = (response.Data is JsonElement jsonElement)
                    ? JsonSerializer.Deserialize<Blockchain[]>(jsonElement.GetRawText())
                    :(Blockchain[])response.Data;
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
internal sealed class CreateCommand : BaseAsyncCommand<CreateCommand.Settings>
{
    public class Settings : VerboseSettings {}
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
        var properties = GetParameters(settings.Verbose);
        properties.Add("blockchain", blockchain);
        AnsiConsole.WriteLine("Sending creation request to the daemon...");
        await EnsureDaemonRunning();
        try
        {
            var response = await CLIClient.clientd.SendCommandAsync("create", properties);
            if (!response.Success)
            {
                AnsiConsole.MarkupLine("[red]Attempt to create a new blockchain by the daemon failed.[/]");
                AnsiConsole.WriteLine($"Daemon's response: {response.Message}");
                return 1;
            }
            if (response.Data == null)
            {
                AnsiConsole.MarkupLine("[yellow]Blockchain created but no id received. Should you wish to know its id, run the 'list' command.[/]");
                return 0;
            }
            Guid id = (response.Data is JsonElement jsonElement)
                    ? JsonSerializer.Deserialize<Guid>(jsonElement.GetRawText())
                    : (Guid)response.Data;
            Console.MarkupLine($"[green]Taxchain creation successful!. Here's its id: {id.ToString()}[/]");
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

internal sealed class FetchCommand : BaseAsyncCommand<FetchCommand.Settings>
{
    public class Settings : VerboseSettings
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
        await EnsureDaemonRunning();
        try
        {
            var properties = GetParameters(settings.Verbose);
            properties.Add("chainId", parsed);
            var response = await CLIClient.clientd.SendCommandAsync("fetch", properties);
            if (!response.Success)
            {
                AnsiConsole.MarkupLine("[red]Fetch unsuccessful.[/]");
                AnsiConsole.WriteLine($"Daemon's message: {response.Message}");
                return 1;
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
    public class Settings : VerboseSettings { }
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        await EnsureDaemonRunning();

        try
        {
            var properties = GetParameters(settings.Verbose);
            var response = await CLIClient.clientd.SendCommandAsync("sync", properties);
            if (!response.Success)
            {
                AnsiConsole.MarkupLine("[red]Synchorisation failed.[/]");
                AnsiConsole.WriteLine($"Daemon's message: ${response.Message}");
                return 1;
            }
            AnsiConsole.MarkupLine($"[green]Taxchains have been synchronised![/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]An exception occured during synchronization.[/]");
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }
}

internal sealed class ConnectCommand : BaseAsyncCommand<ConnectCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        await EnsureDaemonRunning();
        try
        {
            var properties = GetParameters(settings.Verbose);
            if (settings.Host == null || !settings.Port.HasValue)
            {
                AnsiConsole.MarkupLine("[red]Failed to provide necessary arguments.");
                return 1;
            }
            properties.Add("host", settings.Host);
            properties.Add("port", settings.Port);
            var response = await CLIClient.clientd.SendCommandAsync("connect", properties);
            if (!response.Success)
            {
                AnsiConsole.MarkupLine("[red]Connection failed.[/]");
                AnsiConsole.WriteLine($"Daemon's message: ${response.Message}");
                return 1;
            }
            AnsiConsole.MarkupLine($"[green]Peer has been added to the network and will be checked periodically.[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]An exception occured while connecting to peer.[/]");
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    public class Settings : VerboseSettings
    {
        [CommandOption("--host <HOST-IP-OR-ADDRESS-NAME>")]
        public required string? Host { get; set; }
        [CommandOption("--port <PORT>")]
        public required int? Port { get; set; }
    }

}