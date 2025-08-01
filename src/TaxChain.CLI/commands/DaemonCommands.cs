using System.Text.Json;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;
using TaxChain.core;

namespace TaxChain.CLI.commands;

internal sealed class StartCommand : BaseAsyncCommand<StartCommand.StartSetting>
{
    public sealed class StartSetting : CommandSettings {}

    public override async Task<int> ExecuteAsync(CommandContext context, StartSetting settings)
    {
        bool ok = await CLIClient.clientd.StartDaemonAsync();
        if (!ok)
            return 1;
        return 0;
    }
}

internal sealed class KillCommand : BaseAsyncCommand<KillCommand.KillSettings>
{
    public sealed class KillSettings : CommandSettings {}
    public override async Task<int> ExecuteAsync(CommandContext context, KillSettings settings)
    {
        bool ok = await CLIClient.clientd.StopDaemonAsync();
        if (!ok)
            return 1;
        return 0;
    }
}

internal sealed class StatusCommand : BaseAsyncCommand<StatusCommand.Settings>
{
    public sealed class Settings : CommandSettings { }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var statusResponse = await CLIClient.clientd.SendCommandAsync("status");
        if (!statusResponse.Success)
        {
            AnsiConsole.MarkupLine("[red]The daemon is not running.[/]");
            if (statusResponse.Message != "")
                AnsiConsole.WriteLine($"Specific message: {statusResponse.Message}");
            return 1;
        }
        AnsiConsole.MarkupLine("[green]Daemon is running.[/]");
        if (statusResponse.Data != null)
        {
            var data = (JsonElement)statusResponse.Data;
            var statusInfo = data.Deserialize<StatusInformation>();
            if (statusInfo == null)
                return 0;
            AnsiConsole.MarkupLine("[green]Daemon's status:[/]");
            AnsiConsole.WriteLine($"Status: {statusInfo.Status}");
            AnsiConsole.WriteLine($"Process id: {statusInfo.ProcessId}");
            AnsiConsole.WriteLine($"Uptime: {statusInfo.Uptime}");
            AnsiConsole.WriteLine($"TimeStamp: {statusInfo.TimeStamp}");
        }
        return 0;
    }
}

