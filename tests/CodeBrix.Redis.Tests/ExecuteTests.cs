using System.Linq;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class ExecuteTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    [Fact]
    public async Task db_execute()
    {
        await using var conn = Create();

        var db = conn.GetDatabase(4);
        RedisKey key = Me();
        db.StringSet(key, "some value");

        var actual = (string?)db.Execute("GET", key);
        actual.Should().Be("some value");

        actual = (string?)await db.ExecuteAsync("GET", key).ForAwait();
        actual.Should().Be("some value");
    }

    [Fact]
    public async Task server_execute()
    {
        await using var conn = Create();

        var server = conn.GetServer(conn.GetEndPoints().First());
        var actual = (string?)server.Execute("echo", "some value");
        actual.Should().Be("some value");

        actual = (string?)await server.ExecuteAsync("echo", "some value").ForAwait();
        actual.Should().Be("some value");
    }
}
