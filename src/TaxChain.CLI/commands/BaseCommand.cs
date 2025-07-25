using Spectre.Console;
using Spectre.Console.Cli;

namespace TaxChain.CLI.commands
{
    public abstract class BaseCommand<TSettings> : Command<TSettings>
        where TSettings : CommandSettings
    {
        protected readonly IAnsiConsole Console;

        protected BaseCommand()
        {
            Console = AnsiConsole.Console;
        }

        protected void EnsureDaemonRunning()
        {
            // Check if daemon is running, start if needed
            Console.MarkupLine("[yellow]Checking daemon status...[/]");
            // Implementation would check daemon status and start if needed
        }
    }
    
    public abstract class BaseAsyncCommand<TSettings> : AsyncCommand<TSettings>
        where TSettings : CommandSettings
    {
        protected readonly IAnsiConsole Console;

        protected BaseAsyncCommand()
        {
            Console = AnsiConsole.Console;
        }

        protected void EnsureDaemonRunning()
        {
            // Check if daemon is running, start if needed
            Console.MarkupLine("[yellow]Checking daemon status...[/]");
            // Implementation would check daemon status and start if needed
        }
    }
}