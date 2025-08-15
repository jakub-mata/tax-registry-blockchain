using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;

namespace TaxChain.CLI.commands
{
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

        public class VerboseSettings : CommandSettings
        {
            [CommandOption("--verbose")]
            [DefaultValue(false)]
            public bool Verbose { get; set; }
        }

        protected Dictionary<string, object> GetParameters(bool verbose)
        {
            return new Dictionary<string, object>(){
                { "verbose", verbose }
            };
        }

        private async Task<bool> StartDaemon()
        {
            bool ok = await CLIClient.clientd.StartDaemonAsync();
            Thread.Sleep(1000);
            return ok;
        }
    }
}