using System;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[RunPerProtocol]
public class InProcessDatabaseUnitTests(ITestOutputHelper output)
{
    [Fact]
    public async Task databases_are_isolated_and_can_be_flushed()
    {
        TestBase.NoConcurrentRuntime();

        using var server = new InProcessTestServer(output);
        await using var conn = await server.ConnectAsync();

        var admin = conn.GetServer(conn.GetEndPoints()[0]);
        var key = (RedisKey)Guid.NewGuid().ToString("n");
        var db0 = conn.GetDatabase(0);
        var db1 = conn.GetDatabase(1);

        db0.KeyDelete(key, CommandFlags.FireAndForget);
        db1.KeyDelete(key, CommandFlags.FireAndForget);
        db0.StringSet(key, "a");
        db1.StringSet(key, "b");

        db0.StringGet(key).Should().Be("a");
        db1.StringGet(key).Should().Be("b");
        admin.DatabaseSize(0).Should().Be(1);
        admin.DatabaseSize(1).Should().Be(1);

        admin.FlushDatabase(0);
        db0.StringGet(key).IsNull.Should().BeTrue();
        db1.StringGet(key).Should().Be("b");

        admin.FlushAllDatabases();
        db1.StringGet(key).IsNull.Should().BeTrue();
        admin.DatabaseSize(0).Should().Be(0);
        admin.DatabaseSize(1).Should().Be(0);
    }
}
