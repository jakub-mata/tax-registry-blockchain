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
}