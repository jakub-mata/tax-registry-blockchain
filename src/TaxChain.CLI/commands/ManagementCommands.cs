using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;
using TaxChain.core;

namespace TaxChain.CLI.commands;

internal sealed class ListCommand : BaseAsyncCommand<ListCommand.Settings>
{
    public class Settings : CommandSettings {}
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        EnsureDaemonRunning();
        AnsiConsole.MarkupLine("Sending a list request to the chain deamon.[/]");
        try
        {
            var response = await CLIClient.clientd.SendCommandAsync("list");
            if (!response.Success)
            {
                AnsiConsole.MarkupLine("Attempt to list blockchains by the daemon failed.[/]");
                AnsiConsole.MarkupLine($"Daemon's response: {response.Message}[/]");
                return 1;
            }
            if (response.Data != null)
            {
                var blockchains = (Blockchain[])response.Data;
                if (blockchains == null)
                {
                    AnsiConsole.MarkupLine("[red]Failed translate data from the daemon.[/]");
                    return 1;
                }
                string[] columns = { "chainId", "name", "rewardAmount", "difficulty" };
                string[,] rows = new string[blockchains.Length, 4];
                for (int i = 0; i < blockchains.Length; ++i)
                {
                    rows[i, 0] = blockchains[i].Id.ToString();
                    rows[i, 1] = blockchains[i].Name.ToString();
                    rows[i, 2] = blockchains[i].RewardAmount.ToString();
                    rows[i, 3] = blockchains[i].Difficulty.ToString();
                }
                AnsiConsole.Write(TableFactory.CreateTable(columns, rows));
                return 0;
            }
            AnsiConsole.MarkupLine("[red]Daemon has not responded with data. Try again later.[/]");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Failed to list blockchains.[/]");
            AnsiConsole.WriteException(ex);
            return 1;
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
            Console.Markup($"[green]Taxchain creation successful!. Here's its id: {id.ToString()}");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Failed to create a new blockchain.");
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }
}
internal sealed class VerifyCommand : BaseAsyncCommand<VerifyCommand.Settings>
{
    public class Settings : CommandSettings
    {

    }
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        throw new System.NotImplementedException();
    }
}

internal sealed class FetchCommand : BaseAsyncCommand<FetchCommand.Settings>
{
    public class Settings : CommandSettings
    {

    }
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        throw new System.NotImplementedException();
    }
}
internal sealed class SyncCommand : BaseAsyncCommand<SyncCommand.Settings>
{
    public class Settings : CommandSettings
    {

    }
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        throw new System.NotImplementedException();
    }
}