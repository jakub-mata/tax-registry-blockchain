using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;
using TaxChain.core;

namespace TaxChain.CLI.commands;

public class BlockchainSettings : CommandSettings
{
    [CommandArgument(0, "<BLOCKCHAIN_ID>")]
    public string BlockchainId { get; set; } = string.Empty;
}

internal sealed class AddBlockCommand : BaseAsyncCommand<AddBlockCommand.Settings>
{
    public class Settings : BlockchainSettings { }
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        bool ok = Guid.TryParse(settings.BlockchainId, out Guid parsed);
        if (!ok)
        {
            AnsiConsole.MarkupLine("[red]Failed to parse provided chain id. If unsure, run the 'list' command.[/]");
            return 1;
        }
        core.Transaction.TaxType tt = PromptTaxType();
        core.Transaction.TaxStatus ts = PromptStatus();
        string taxpayerId = AnsiConsole.Prompt(
            new TextPrompt<string>("Type your taxpayer id:")
        );
        decimal amount = AnsiConsole.Prompt<decimal>(
            new TextPrompt<decimal>("Write the amount:")
        );
        decimal tbase = AnsiConsole.Prompt<decimal>(
            new TextPrompt<decimal>("Write the taxable base:")
        );
        decimal trate = AnsiConsole.Prompt<decimal>(
            new TextPrompt<decimal>("Write the tax rate:")
        );
        int currYear = DateTime.Now.Year;
        DateTime periodStart = PromptDateTime(
            "Select the start of the tax period",
            currYear - 50, currYear + 5
        );
        DateTime periodEnd = PromptDateTime(
            "Select the end of the tax period",
            currYear - 10, currYear + 50
        );
        DateTime dueDate = PromptDateTime(
            "Select the tax due date",
            currYear - 10, currYear + 50
        );
        string jurisdiction = AnsiConsole.Prompt<string>(
            new TextPrompt<string>("Provide the jurisdiction:")
        );
        string notes = AnsiConsole.Prompt<string>(
            new TextPrompt<string>("Add any notes you find necessary and hit [Enter] to finish.")
        );
        var transaction = new core.Transaction(){
            TaxpayerId = taxpayerId,
            Amount = amount,
            TaxableBase = tbase,
            TaxRate = trate,
            TaxPeriodStart = periodStart,
            TaxPeriodEnd = periodEnd,
            DueDate = dueDate,
            Jurisdiction = jurisdiction,
            Status = ts,
            Type = tt,
            Notes = notes
        };
        return await SendAddRequest(transaction, parsed);
    }

    private async Task<int> SendAddRequest(core.Transaction t, Guid chainId)
    {
        EnsureDaemonRunning();
        var properties = new Dictionary<string, object>()
        {
            {"chainId", chainId},
            { "transaction", t},
        };
        AnsiConsole.WriteLine("Sending provided transaction to the records...");
        try
        {
            var response = await CLIClient.clientd.SendCommandAsync("add");
            if (!response.Success)
            {
                AnsiConsole.MarkupLine("[red]Failed to add block to taxchain.[/]");
                AnsiConsole.WriteLine($"Daemon's message: {response.Message}");
                return 1;
            }
            AnsiConsole.MarkupLine($"[green]Successfully added a block to chain {chainId}![/]");
            AnsiConsole.MarkupLine($"[yellow]If you want to ensure it gets appended, run the 'mine' command.[/]");
            return 0; 
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Exception occured while adding a new block.[/]");
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private static DateTime PromptDateTime(string promptText, int startYear, int endYear)
    {
        AnsiConsole.WriteLine(promptText);
        var months = Enumerable.Range(1, 12);
        var days = Enumerable.Range(1, 31);
        var years = Enumerable.Range(startYear, endYear);
        var month = AnsiConsole.Prompt<int>(
            new SelectionPrompt<int>()
                .Title("Select the month:")
                .PageSize(12)
                .AddChoices<int>(months)
        );
        var day = AnsiConsole.Prompt<int>(
             new SelectionPrompt<int>()
                 .Title("Select the day:")
                 .PageSize(12)
                 .AddChoices<int>(days)
         );
        var year = AnsiConsole.Prompt<int>(
            new SelectionPrompt<int>()
                .Title("Select the year:")
                .PageSize(10)
                .MoreChoicesText("Move up or down to show more options")
                .AddChoices<int>(years)
        );
        return new DateTime(year, month, day);
    }

    private static core.Transaction.TaxType PromptTaxType()
    {
        var tt = AnsiConsole.Prompt(
            new SelectionPrompt<core.Transaction.TaxType>()
                .Title("Choose tax type:")
                .PageSize(10)
                .MoreChoicesText("Move up and down to reveal more types.")
                .AddChoices([
                    core.Transaction.TaxType.CapitalGainsTax,
                    core.Transaction.TaxType.CarbonTax,
                    core.Transaction.TaxType.CorporateIncomeTax,
                    core.Transaction.TaxType.DividendTax,
                    core.Transaction.TaxType.ExciseTax,
                    core.Transaction.TaxType.FuelTax,
                    core.Transaction.TaxType.GiftTax,
                    core.Transaction.TaxType.InheritanceTax,
                    core.Transaction.TaxType.InterestIncomeTax,
                    core.Transaction.TaxType.LuxuryTax,
                    core.Transaction.TaxType.MedicareTax,
                    core.Transaction.TaxType.PayrollTax,
                    core.Transaction.TaxType.PersonalIncomeTax,
                    core.Transaction.TaxType.PropertyTax,
                    core.Transaction.TaxType.SalesTax,
                    core.Transaction.TaxType.ServiceTax,
                    core.Transaction.TaxType.SinTax,
                    core.Transaction.TaxType.SocialSecurityTax,
                    core.Transaction.TaxType.SugarTax,
                    core.Transaction.TaxType.UnemploymentTax,
                    core.Transaction.TaxType.VAT,
                    core.Transaction.TaxType.VehicleTax,
                    core.Transaction.TaxType.WealthTax,
                    core.Transaction.TaxType.WorkersCompensation,
                ])
        );
        return tt;
    }

    private static core.Transaction.TaxStatus PromptStatus()
    {
        var taxpayerId = AnsiConsole.Prompt(
            new SelectionPrompt<core.Transaction.TaxStatus>()
                .Title("Choose tax status:")
                .PageSize(7)
                .MoreChoicesText("Move up and down to reveal more types.")
                .AddChoices([
                    core.Transaction.TaxStatus.Delinquent,
                    core.Transaction.TaxStatus.Disputed,
                    core.Transaction.TaxStatus.Filed,
                    core.Transaction.TaxStatus.Paid,
                    core.Transaction.TaxStatus.PartiallyPaid,
                    core.Transaction.TaxStatus.Refunded,
                    core.Transaction.TaxStatus.UnderAudit,
                ])
        );
        return taxpayerId;
    }
}
internal sealed class GatherCommand : BaseAsyncCommand<GatherCommand.Settings>
{
    public class Settings : BlockchainSettings
    {
        [CommandOption("-u|--user <USER_ADDRESS>")]
        public string? UserAddress { get; set; }
        [CommandOption("-v|--verbose")]
        public bool? Verbose { get; set; }
    }
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var properties = new Dictionary<string, object>();
        if (settings.UserAddress == null || settings.BlockchainId == null)
        {
            AnsiConsole.MarkupLine("[red]Failed to provide necessary options.[/]");
            return 1;
        }
        properties.Add("taxpayerId", settings.UserAddress);
        properties.Add("verbose", settings.Verbose == true);
        properties.Add("chainId", Guid.Parse(settings.BlockchainId));

        EnsureDaemonRunning();
        AnsiConsole.WriteLine("Sending a request for the gather command...");
        try
        {
            var response = await CLIClient.clientd.SendCommandAsync("gather", properties);
            if (!response.Success)
            {
                AnsiConsole.MarkupLine("[red]Failed to gather taxpayer information. Try again later.[/]");
                AnsiConsole.WriteLine($"Daemon's message: {response.Message}");
                return 1;
            }
            if (response.Data == null)
            {
                AnsiConsole.MarkupLine("[red]Daemon did not send any data. Try again later.[/]");
                return 1;
            }
            List<Transaction>? info = (List<Transaction>)response.Data;
            if (info == null)
            {
                AnsiConsole.MarkupLine("[red]Could not parse received data.[/]");
                return 1;
            }

            DisplayTaxpayerTable(info, settings.Verbose == true);
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Exception occured while gathering taxpayer data.[/]");
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private static void DisplayTaxpayerTable(List<Transaction> data, bool verbose)
    {
        foreach (Transaction t in data)
        {
            AnsiConsole.MarkupLine($"ID: [green]{t.TaxpayerId}[/]");
            AnsiConsole.MarkupLine($"Amount: [green]{t.Amount}[/]");
        }
    }
}
internal sealed class LedgerCommand : BaseAsyncCommand<LedgerCommand.Settings>
{
    private readonly int _defaultNumber = 5;
    public class Settings : BlockchainSettings
    {
        [CommandOption("-n|--number <AMOUNT>")]
        public int? Number { get; set; }
    }
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        settings.Number ??= _defaultNumber;
        var parameters = new Dictionary<string, object>()
        {
            {"number", settings.Number},
            {"chainId", Guid.Parse(settings.BlockchainId)}
        };
        EnsureDaemonRunning();
        AnsiConsole.WriteLine($"Sending a request for a ledger of size {settings.Number}");
        try
        {
            var response = await CLIClient.clientd.SendCommandAsync("ledger", parameters);
            if (!response.Success)
            {
                AnsiConsole.MarkupLine("[red]Failed to fetch ledger. Try again later.[/]");
                AnsiConsole.WriteLine($"Daemon's message: {response.Message}");
                return 1;
            }
            if (response.Data == null)
            {
                AnsiConsole.MarkupLine("[red]Daemon did not send any data. Try again later.[/]");
                return 1;
            }
            Block[]? blocks = (Block[]?)response.Data;
            if (blocks == null)
            {
                AnsiConsole.MarkupLine("[red]Unable to parse received data.[/]");
                return 1;
            }
            AnsiConsole.Write(TableFactory.CreateLedgerTable(blocks));
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Exception occured while fetching the ledger.[/]");
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }
}