using Spectre.Console;

namespace TaxChain.CLI;

public class TableFactory
{
    public static Table CreateTable(string[] columns, string[,] rowsMatrix)
    {
        var table = new Table();
        table.AddColumns(columns);
        foreach (var row in rowsMatrix)
            table.AddRow(row);
        return table;
    }

    public static Table CreateTransactionsTable(core.Transaction[] transactions)
    {
        string[] columns = {
            "TaxpayerId",
            "Amount",
            "TaxableBase",
            "TaxRate",
            "TaxPeriodStart",
            "TaxPeriodEnd",
            "DueDate",
            "PaymentDate",
            "Jurisdiction",
            "Status",
            "Notes",
            "Type",
            "Status",
        };
        string[,] rowsMatrix = new string[transactions.Length, columns.Length];
        for (int i = 0; i < transactions.Length; ++i)
        {
            rowsMatrix[i, 0] = transactions[i].TaxpayerId;
            rowsMatrix[i, 1] = transactions[i].Amount.ToString();
            rowsMatrix[i, 2] = transactions[i].TaxableBase.ToString();
            rowsMatrix[i, 3] = transactions[i].TaxPeriodStart.ToString();
            rowsMatrix[i, 4] = transactions[i].TaxPeriodEnd.ToString();
            rowsMatrix[i, 5] = transactions[i].DueDate.ToString();
            rowsMatrix[i, 6] = transactions[i].PaymentDate.ToString();
            rowsMatrix[i, 7] = transactions[i].Jurisdiction;
            rowsMatrix[i, 8] = transactions[i].Notes.ToString();
            rowsMatrix[i, 9] = transactions[i].Type.ToString();
            rowsMatrix[i, 10] = transactions[i].Status.ToString();
        }
        return CreateTable(columns, rowsMatrix);
    }

    public static Table CreateLedgerTable(core.Block[] blocks)
    {
        string[] columns = {
            "BlockID",
            "PrevHash",
            "Hash",
            "Nonce",
            "TaxpayerId",
            "Amount",
            "TaxableBase",
            "TaxRate",
            "TaxPeriodStart",
            "TaxPeriodEnd",
            "DueDate",
            "PaymentDate",
            "Jurisdiction",
            "Status",
            "Notes",
            "Type",
            "Status",
        };
        string[,] rowsMatrix = new string[blocks.Length, columns.Length];
        for (int i = 0; i < blocks.Length; ++i)
        {
            rowsMatrix[i, 0] = blocks[i].Id.ToString();
            rowsMatrix[i, 1] = blocks[i].PreviousHash ?? "unknown";
            rowsMatrix[i, 2] = blocks[i].Hash;
            rowsMatrix[i, 3] = blocks[i].Nonce.ToString();
            rowsMatrix[i, 4] = blocks[i].Payload.TaxpayerId;
            rowsMatrix[i, 5] = blocks[i].Payload.Amount.ToString();
            rowsMatrix[i, 6] = blocks[i].Payload.TaxableBase.ToString();
            rowsMatrix[i, 7] = blocks[i].Payload.TaxPeriodStart.ToString();
            rowsMatrix[i, 8] = blocks[i].Payload.TaxPeriodEnd.ToString();
            rowsMatrix[i, 9] = blocks[i].Payload.DueDate.ToString();
            rowsMatrix[i, 10] = blocks[i].Payload.PaymentDate.ToString();
            rowsMatrix[i, 11] = blocks[i].Payload.Jurisdiction;
            rowsMatrix[i, 12] = blocks[i].Payload.Notes.ToString();
            rowsMatrix[i, 13] = blocks[i].Payload.Type.ToString();
            rowsMatrix[i, 14] = blocks[i].Payload.Status.ToString();
        }
        return CreateTable(columns, rowsMatrix);
    }
}