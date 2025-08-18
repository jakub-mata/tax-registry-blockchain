using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;

namespace TaxChain.CLI.commands
{
    /// <summary>
    /// Base class for all async commands in the CLI.
    /// Provides common functionality such as starting the daemon and handling verbose settings.
    /// </summary>
    /// <typeparam name="TSettings"></typeparam>
    public abstract class BaseAsyncCommand<TSettings> : AsyncCommand<TSettings>
    where TSettings : CommandSettings
    {
        protected readonly IAnsiConsole Console;

        protected BaseAsyncCommand()
        {
            Console = AnsiConsole.Console;
        }

        /// <summary>
        /// Ensures that the daemon is running before executing the command.
        /// If the daemon is not running, it attempts to start it.
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Base class for command settings that includes a verbose option.
        /// This allows commands to provide more detailed output when requested.
        /// </summary>
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