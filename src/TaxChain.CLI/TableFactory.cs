using System;
using System.Collections.Generic;
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
            if (rowsMatrix[i] == null || rowsMatrix[i].Length != columns.Length)
                throw new InvalidOperationException("Row length does not match column length");
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

    public static Table CreateLedgerTable(List<core.Block> blocks)
    {
        string[] columns = {
            "PrevHash",
            "Hash",
            "Nonce"
        };
        string[][] rowsMatrix = new string[blocks.Count][];
        for (int i = 0; i < blocks.Count; ++i)
        {
            rowsMatrix[i] = [
                blocks[i].PreviousHash,
                blocks[i].Hash,
                blocks[i].Nonce.ToString()
            ];
        }
        return CreateTable(columns, rowsMatrix);
    }
}