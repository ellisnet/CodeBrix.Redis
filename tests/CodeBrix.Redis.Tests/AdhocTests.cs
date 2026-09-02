using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class AdhocTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    [Fact]
    public async Task test_adhoc_commands_api()
    {
        await using var conn = Create();
        var db = conn.GetDatabase();

        // needs explicit RedisKey type for key-based
        // sharding to work; will still work with strings,
        // but no key-based sharding support
        RedisKey key = Me();

        // note: if command renames are configured in
        // the API, they will still work automatically
        db.Execute("del", key);
        db.Execute("set", key, "12");
        db.Execute("incrby", key, 4);
        int i = (int)db.Execute("get", key);

        i.Should().Be(16);
    }
}
