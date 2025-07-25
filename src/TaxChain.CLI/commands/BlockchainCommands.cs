using Spectre.Console.Cli;

namespace TaxChain.CLI.commands;

public class BlockchainSettings : CommandSettings
{
    [CommandArgument(0, "<BLOCKCHAIN_ID>")]
    public string BlockchainId { get; set; } = string.Empty;
}

internal sealed class AddBlockCommand : BaseCommand<AddBlockCommand.Settings>
{
    public class Settings : BlockchainSettings {}
    public override int Execute(CommandContext context, Settings settings)
    {
        throw new System.NotImplementedException();
    }
}
internal sealed class GatherCommand : BaseCommand<GatherCommand.Settings>
{
    public class Settings : BlockchainSettings
    {
        [CommandOption("-u|--user <USER_ADDRESS>")]
        public string? UserAddress { get; set; }
    }
    public override int Execute(CommandContext context, Settings settings)
    {
        throw new System.NotImplementedException();
    }
}
internal sealed class LedgerCommand : BaseCommand<LedgerCommand.Settings>
{
    public class Settings : BlockchainSettings
    {
        [CommandOption("-n|--number <AMOUNT>")]
        public int? Number { get; set; }
            
        [CommandOption("-a|--all")]
        public bool All { get; set; }
    }
    public override int Execute(CommandContext context, Settings settings)
    {
        throw new System.NotImplementedException();
    }
}