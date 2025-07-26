using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;

namespace TaxChain.CLI.commands;

internal sealed class StartCommand : BaseAsyncCommand<StartCommand.StartSetting>
{
    public sealed class StartSetting : CommandSettings {}

    public override async Task<int> ExecuteAsync(CommandContext context, StartSetting settings)
    {
        bool ok = await CLIClient.clientd.StartDaemonAsync();
        if (!ok)
        {
            AnsiConsole.MarkupLine("[red]The daemon failed to start up. Try again later.[/]");
            return 0;
        }
        AnsiConsole.MarkupLine("[green]The daemon is running.[/]");
        return 1;
    }
}

internal sealed class KillCommand : BaseAsyncCommand<KillCommand.KillSettings>
{
    public sealed class KillSettings : CommandSettings {}
    public override async Task<int> ExecuteAsync(CommandContext context, KillSettings settings)
    {
        bool ok = await CLIClient.clientd.StopDaemonAsync();
        if (!ok)
        {
            AnsiConsole.MarkupLine("[red]The daemon failed to stop. Try again later.[/]");
            return 0;
        }
        AnsiConsole.MarkupLine("[green]The daemon has been stopped.[/]");
        return 1;
    }
}

internal sealed class StatusCommand : BaseAsyncCommand<StatusCommand.Settings>
{
    public sealed class Settings : CommandSettings { }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var statusResponse = await CLIClient.clientd.SendCommandAsync("status");
        if (statusResponse.Success)
        {
            AnsiConsole.MarkupLine("The daemon is currently [green]running.[/]");
            return 0;
        }
        AnsiConsole.MarkupLine("The daemon is [red]not running.[/]");
        return 0;
    }
}

