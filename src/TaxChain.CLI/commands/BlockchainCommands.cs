using System.Threading.Tasks;
using Spectre.Console.Cli;

namespace TaxChain.CLI.commands;

public class BlockchainSettings : CommandSettings
{
    [CommandArgument(0, "<BLOCKCHAIN_ID>")]
    public string BlockchainId { get; set; } = string.Empty;
}

internal sealed class AddBlockCommand : BaseAsyncCommand<AddBlockCommand.Settings>
{
    public class Settings : BlockchainSettings {}
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        throw new System.NotImplementedException();
    }
}
internal sealed class GatherCommand : BaseAsyncCommand<GatherCommand.Settings>
{
    public class Settings : BlockchainSettings
    {
        [CommandOption("-u|--user <USER_ADDRESS>")]
        public string? UserAddress { get; set; }
    }
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        throw new System.NotImplementedException();
    }
}
internal sealed class LedgerCommand : BaseAsyncCommand<LedgerCommand.Settings>
{
    public class Settings : BlockchainSettings
    {
        [CommandOption("-n|--number <AMOUNT>")]
        public int? Number { get; set; }
            
        [CommandOption("-a|--all")]
        public bool All { get; set; }
    }
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        throw new System.NotImplementedException();
    }
}