using System;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.Issues; //was previously: StackExchange.Redis.Tests.Issues;

public class SO24807536Tests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task exec()
    {
        await using var conn = Create();

        var key = Me();
        var db = conn.GetDatabase();

        // setup some data
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.HashSet(key, "full", "some value", flags: CommandFlags.FireAndForget);
        db.KeyExpire(key, TimeSpan.FromSeconds(2), CommandFlags.FireAndForget);

        // test while exists
        var keyExists = db.KeyExists(key);
        var ttl = db.KeyTimeToLive(key);
        var fullWait = db.HashGetAsync(key, "full", flags: CommandFlags.None);
        keyExists.Should().BeTrue("key exists");
        ttl.Should().NotBeNull();
        (await fullWait).Should().Be("some value");

        // wait for expiry
        await UntilConditionAsync(TimeSpan.FromSeconds(10), () => !db.KeyExists(key)).ForAwait();

        // test once expired
        keyExists = db.KeyExists(key);
        ttl = db.KeyTimeToLive(key);
        fullWait = db.HashGetAsync(key, "full", flags: CommandFlags.None);

        keyExists.Should().BeFalse();
        ttl.Should().BeNull();
        var r = await fullWait;
        r.IsNull.Should().BeTrue();
        ((string?)r).Should().BeNull();
    }
}
