using System.Threading.Tasks;
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

        protected async void EnsureDaemonRunning()
        {
            // Check if daemon is running, start if needed
            Console.MarkupLine("[yellow]Checking daemon status...[/]");
            var statusResponse = await CLIClient.clientd.SendCommandAsync("status");
            if (!statusResponse.Success)
            {
                AnsiConsole.MarkupLine("[yellow]Daemon not running, starting now...[/]");
                bool ok = await StartDaemon();
                if (!ok)
                    AnsiConsole.MarkupLine("[red]Failed to start up the daemon, try again later...[/]");
                else
                    AnsiConsole.MarkupLine("[green]Daemon is running now.");
                return;
            }
        }

        private async Task<bool> StartDaemon()
        {
            return await CLIClient.clientd.StartDaemonAsync();
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

        protected async Task EnsureDaemonRunning()
        {
            // Check if daemon is running, start if needed
            Console.MarkupLine("[yellow]Checking daemon status...[/]");
            var statusResponse = await CLIClient.clientd.SendCommandAsync("status");
            if (!statusResponse.Success)
            {
                AnsiConsole.MarkupLine("[yellow]Daemon not running, starting now...[/]");
                bool ok = await StartDaemon();
                if (!ok)
                    AnsiConsole.MarkupLine("[red]Failed to start up the daemon, try again later...[/]");
                else
                    AnsiConsole.MarkupLine("[green]Daemon is running now.[/]");
                return;
            }
        }

        private async Task<bool> StartDaemon()
        {
            return await CLIClient.clientd.StartDaemonAsync();
        }
    }
}