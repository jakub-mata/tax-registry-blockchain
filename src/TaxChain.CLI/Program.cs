using System;

namespace TaxChain.CLI;

class Program
{
    static void Main(string[] args)
    {
        int task = CLIClient.Run(args);
        // Handle the task return status
    }
}
