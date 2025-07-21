using System;
using System.Collections.Generic;
using System.IO;    
using Spectre.Console;
using Spectre.Console.Cli;

namespace tax_registry_blockchain.cli.commands;

internal sealed class ListCommand : Command<ListCommand.Settings>
{
    public sealed class Settings : CommandSettings { }

    public override int Execute(CommandContext context, Settings settings)
    {
        string directory = "./blockchain";
        if (!Directory.Exists(directory))
        {
            AnsiConsole.MarkupLine("[yellow]No blockchain files available.[/]");
            return 1;
        }

        var fileChains = CLIClient.StorageManager.FileStore.List();
        var dbChains = CLIClient.StorageManager.DBStore.List();
        if (fileChains.Count == 0 && dbChains.Count == 0)
            return 0;

        AnsiConsole.Write(CreateTable(fileChains, dbChains));
        return 0;
    }

    private Spectre.Console.Table CreateTable(List<Tuple<string, Guid>> fileChains, List<Tuple<string, Guid>> dbChains)
    {
        var table = new Table();
        table.AddColumn("In files");
        table.AddColumn("In database");
        int maxIndex = Math.Max(fileChains.Count, dbChains.Count);
        for (int i = 0; i < maxIndex; ++i)
        {
            string fileChain = i < fileChains.Count
                ? fileChains[i].Item1 + '/' + fileChains[i].Item2.ToString()
                : "";
            string dbChain = i < dbChains.Count
                ? dbChains[i].Item1 + '/' + dbChains[i].Item2.ToString()
                : "";
            table.AddRow(fileChain, dbChain);
        }
        return table;
    }
}