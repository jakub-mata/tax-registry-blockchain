using System;
using Spectre.Console;
using Spectre.Console.Cli;
using TaxChain.CLI.commands;
using TaxChain.core;

namespace TaxChain.CLI
{
    public static class CLIClient
    {
        public static Services.DaemonClient clientd = new Services.DaemonClient();
        public static int Run(string[] args)
        {
            var app = new CommandApp();

            app.Configure(config =>
            {
                config.SetApplicationName("taxchain");

                // Daemon commands
                config.AddCommand<StartCommand>("start")
                    .WithDescription("Starts the daemon");
                config.AddCommand<KillCommand>("kill")
                    .WithDescription("Kills the daemon");
                config.AddCommand<StatusCommand>("status")
                    .WithDescription("Displays daemon's status");

                // Blockchain management commands
                config.AddCommand<ListCommand>("list")
                    .WithDescription("Lists all blockchains stored locally");
                config.AddCommand<SyncCommand>("sync")
                    .WithDescription("Synchronizes blockchain(s) with the network");
                config.AddCommand<RemoveCommand>("remove")
                    .WithDescription("Removes a blockchain from local storage");
                config.AddCommand<CreateCommand>("create")
                    .WithDescription("Creates a new blockchain");
                config.AddCommand<FetchCommand>("fetch")
                    .WithDescription("Adds a blockchain from the network");
                config.AddCommand<VerifyCommand>("verify")
                    .WithDescription("Verifies the validity of a blockchain");

                // Blockchain-specific commands (nested under blockchain ID)
                config.AddBranch<BlockchainSettings>("blockchain", blockchain =>
                {
                    blockchain.SetDescription("Blockchain-specific operations");
                    blockchain.AddCommand<AddBlockCommand>("add")
                        .WithDescription("Add a new block to the blockchain");
                    blockchain.AddCommand<GatherCommand>("gather")
                        .WithDescription("Get balances of all users");
                    blockchain.AddCommand<LedgerCommand>("ledger")
                        .WithDescription("Lists blocks in the blockchain");
                });
            });

            try
            {
                return app.Run(args);
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
                return -1;
            }
        }
    }
}