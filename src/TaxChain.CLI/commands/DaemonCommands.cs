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
        if (statusResponse.Success)
            return 1;
        return 0;
    }
}

