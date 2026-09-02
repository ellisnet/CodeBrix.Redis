using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class HighIntegrityMovedUnitTests(ITestOutputHelper log)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task high_integrity_survives_moved_response(bool highIntegrity)
    {
        using var server = new InProcessTestServer(log) { ServerType = ServerType.Cluster };
        var secondary = server.AddEmptyNode();

        var config = server.GetClientConfig();
        config.HighIntegrity = highIntegrity;
        using var client = await ConnectionMultiplexer.ConnectAsync(config);

        RedisKey a = "a", b = "b"; // known to be in different slots
        ServerSelectionStrategy.GetHashSlot(b).Should().NotBe(ServerSelectionStrategy.GetHashSlot(a));

        var db = client.GetDatabase();
        var x = db.StringIncrementAsync(a);
        var y = db.StringIncrementAsync(b);
        await x;
        await y;
        (await db.StringGetAsync(a)).Should().Be(1);
        (await db.StringGetAsync(b)).Should().Be(1);

        // now force a -MOVED response
        server.Migrate(a, secondary);
        x = db.StringIncrementAsync(a);
        y = db.StringIncrementAsync(b);
        await x;
        await y;
        (await db.StringGetAsync(a)).Should().Be(2);
        (await db.StringGetAsync(b)).Should().Be(2);
    }
}
