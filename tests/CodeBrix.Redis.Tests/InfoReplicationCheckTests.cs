using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class InfoReplicationCheckTests(ITestOutputHelper output) : TestBase(output)
{
    protected override string GetConfiguration() => base.GetConfiguration() + ",configCheckSeconds=2";

    [Fact]
    public async Task exec()
    {
        Assert.Skip("need to think about CompletedSynchronously");

        await using var conn = Create();

        var parsed = ConfigurationOptions.Parse(conn.Configuration);
        parsed.ConfigCheckSeconds.Should().Be(2);
        var before = conn.GetCounters();
        await Task.Delay(7000, TestContext.Current.CancellationToken).ForAwait();
        var after = conn.GetCounters();
        int done = (int)(after.Interactive.CompletedSynchronously - before.Interactive.CompletedSynchronously);
        (done >= 2).Should().BeTrue($"expected >=2, got {done}");
    }
}
