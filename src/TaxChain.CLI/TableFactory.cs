using System;
using Spectre.Console;

namespace TaxChain.CLI;

public class TableFactory
{
    public static Table CreateTable(string[] columns, string[][] rowsMatrix)
    {
        var table = new Table();
        table.AddColumns(columns);
        for (int i = 0; i < rowsMatrix.Length; ++i)
        {
            table.AddRow(rowsMatrix[i]);
        }
        return table;
    }

    public static Table CreateTransactionsTable(core.Transaction[] transactions)
    {
        string[] columns = {
            "TaxpayerId",
            "Amount",
        };
        string[][] rowsMatrix = new string[transactions.Length][];
        for (int i = 0; i < transactions.Length; ++i)
        {
            rowsMatrix[i] = [transactions[i].TaxpayerId, transactions[i].Amount.ToString()];
        }
        return CreateTable(columns, rowsMatrix);
    }

    public static Table CreateLedgerTable(core.Block[] blocks)
    {
        string[] columns = {
            "PrevHash",
            "Hash",
            "Nonce",
            "TaxpayerId",
            "Amount",
        };
        string[][] rowsMatrix = new string[blocks.Length][];
        for (int i = 0; i < blocks.Length; ++i)
        {
            rowsMatrix[i] = [
                blocks[i].PreviousHash ?? "unknown",
                blocks[i].Hash,
                blocks[i].Nonce.ToString(),
                blocks[i].Payload.TaxpayerId,
                blocks[i].Payload.Amount.ToString()
            ];
        }
        return CreateTable(columns, rowsMatrix);
    }
}