using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.Issues; //was previously: StackExchange.Redis.Tests.Issues;

public class SO25113323Tests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task set_expiration_to_passed()
    {
        await using var conn = Create();

        // Given
        var key = Me();
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.HashSet(key, "full", "test", When.NotExists, CommandFlags.PreferMaster);

        await Task.Delay(2000, TestContext.Current.CancellationToken).ForAwait();

        // When
        var serverTime = GetServer(conn).Time();
        var expiresOn = serverTime.AddSeconds(-2);

        var firstResult = db.KeyExpire(key, expiresOn, CommandFlags.PreferMaster);
        var secondResult = db.KeyExpire(key, expiresOn, CommandFlags.PreferMaster);
        var exists = db.KeyExists(key);
        var ttl = db.KeyTimeToLive(key);

        // Then
        firstResult.Should().BeTrue(); // could set the first time, but this nukes the key
        secondResult.Should().BeFalse(); // can't set, since nuked
        exists.Should().BeFalse(); // does not exist since nuked
        ttl.Should().BeNull(); // no expiry since nuked
    }
}
