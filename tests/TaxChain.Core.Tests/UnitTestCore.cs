using TaxChain.core;

namespace TaxChain.Core.Tests;

public class CoreTest
{
    [Fact]
    public void TestMineDiff3()
    {
        int diff = 3;
        CancellationToken token = new CancellationToken();
        Block block = new Block(Guid.NewGuid(), "somepreviousHash", new Transaction("Me", 234.345m));
        block.Mine(diff, token);

        Assert.StartsWith(new string('0', diff), block.Hash);
    }

    [Fact]
    public void TestMineDiff4()
    {
        int diff = 4;
        CancellationToken token = new CancellationToken();
        Block block = new Block(Guid.NewGuid(), "somepreviousHash", new Transaction("Me", 234.345m));
        block.Mine(diff, token);

        Assert.StartsWith(new string('0', diff), block.Hash);
    }
}