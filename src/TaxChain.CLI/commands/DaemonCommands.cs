using Spectre.Console.Cli;

namespace TaxChain.CLI.commands;

internal sealed class StartCommand : BaseCommand<StartCommand.StartSetting>
{
    public sealed class StartSetting : CommandSettings
    {

    }

    public override int Execute(CommandContext context, StartSetting settings)
    {
        throw new System.NotImplementedException();
    }
}

internal sealed class KillCommand : BaseCommand<KillCommand.KillSettings>
{
    public sealed class KillSettings : CommandSettings
    {

    }
    public override int Execute(CommandContext context, KillSettings settings)
    {
        throw new System.NotImplementedException();
    }
}

internal sealed class StatusCommand : BaseCommand<StatusCommand.Settings>
{
    public sealed class Settings : CommandSettings { }

    public override int Execute(CommandContext context, Settings settings)
    {
        throw new System.NotImplementedException();
    }
}

