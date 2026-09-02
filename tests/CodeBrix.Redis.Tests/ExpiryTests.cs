using System;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class ExpiryTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    private static string[]? GetMap(bool disablePTimes) => disablePTimes ? ["pexpire", "pexpireat", "pttl"] : null;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task test_basic_expiry_time_span(bool disablePTimes)
    {
        await using var conn = Create(disabledCommands: GetMap(disablePTimes));

        RedisKey key = Me();
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        db.StringSet(key, "new value", flags: CommandFlags.FireAndForget);
        var a = db.KeyTimeToLiveAsync(key);
        db.KeyExpire(key, TimeSpan.FromHours(1), CommandFlags.FireAndForget);
        var b = db.KeyTimeToLiveAsync(key);
        db.KeyExpire(key, (TimeSpan?)null, CommandFlags.FireAndForget);
        var c = db.KeyTimeToLiveAsync(key);
        db.KeyExpire(key, TimeSpan.FromHours(1.5), CommandFlags.FireAndForget);
        var d = db.KeyTimeToLiveAsync(key);
        db.KeyExpire(key, TimeSpan.MaxValue, CommandFlags.FireAndForget);
        var e = db.KeyTimeToLiveAsync(key);
        db.KeyDelete(key, CommandFlags.FireAndForget);
        var f = db.KeyTimeToLiveAsync(key);

        (await a).Should().BeNull();
        var time = await b;
        Assert.NotNull(time);
        (time > TimeSpan.FromMinutes(59.9) && time <= TimeSpan.FromMinutes(60)).Should().BeTrue();
        (await c).Should().BeNull();
        time = await d;
        Assert.NotNull(time);
        (time > TimeSpan.FromMinutes(89.9) && time <= TimeSpan.FromMinutes(90)).Should().BeTrue();
        (await e).Should().BeNull();
        (await f).Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task test_expiry_options(bool disablePTimes)
    {
        await using var conn = Create(disabledCommands: GetMap(disablePTimes), require: RedisFeatures.v7_0_0_rc1);

        var key = Me();
        var db = conn.GetDatabase();
        db.KeyDelete(key);
        db.StringSet(key, "value");

        // The key has no expiry
        (await db.KeyExpireAsync(key, TimeSpan.FromHours(1), ExpireWhen.HasExpiry)).Should().BeFalse();
        (await db.KeyExpireAsync(key, TimeSpan.FromHours(1), ExpireWhen.HasNoExpiry)).Should().BeTrue();

        // The key has an existing expiry
        (await db.KeyExpireAsync(key, TimeSpan.FromHours(1), ExpireWhen.HasExpiry)).Should().BeTrue();
        (await db.KeyExpireAsync(key, TimeSpan.FromHours(1), ExpireWhen.HasNoExpiry)).Should().BeFalse();

        // Set only when the new expiry is greater than current one
        (await db.KeyExpireAsync(key, TimeSpan.FromHours(1.5), ExpireWhen.GreaterThanCurrentExpiry)).Should().BeTrue();
        (await db.KeyExpireAsync(key, TimeSpan.FromHours(0.5), ExpireWhen.GreaterThanCurrentExpiry)).Should().BeFalse();

        // Set only when the new expiry is less than current one
        (await db.KeyExpireAsync(key, TimeSpan.FromHours(0.5), ExpireWhen.LessThanCurrentExpiry)).Should().BeTrue();
        (await db.KeyExpireAsync(key, TimeSpan.FromHours(1.5), ExpireWhen.LessThanCurrentExpiry)).Should().BeFalse();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task test_basic_expiry_date_time(bool disablePTimes, bool utc)
    {
        await using var conn = Create(disabledCommands: GetMap(disablePTimes));

        RedisKey key = Me();
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        var now = utc ? DateTime.UtcNow : DateTime.Now;
        var serverTime = GetServer(conn).Time();
        Log("Server time: {0}", serverTime);
        var offset = DateTime.UtcNow - serverTime;

        Log("Now (local time): {0}", now);
        db.StringSet(key, "new value", flags: CommandFlags.FireAndForget);
        var a = db.KeyTimeToLiveAsync(key);
        db.KeyExpire(key, now.AddHours(1), CommandFlags.FireAndForget);
        var b = db.KeyTimeToLiveAsync(key);
        db.KeyExpire(key, (DateTime?)null, CommandFlags.FireAndForget);
        var c = db.KeyTimeToLiveAsync(key);
        db.KeyExpire(key, now.AddHours(1.5), CommandFlags.FireAndForget);
        var d = db.KeyTimeToLiveAsync(key);
        db.KeyExpire(key, DateTime.MaxValue, CommandFlags.FireAndForget);
        var e = db.KeyTimeToLiveAsync(key);
        db.KeyDelete(key, CommandFlags.FireAndForget);
        var f = db.KeyTimeToLiveAsync(key);

        (await a).Should().BeNull();
        var timeResult = await b;
        Assert.NotNull(timeResult);
        TimeSpan time = timeResult.Value;

        // Adjust for server time offset, if any when checking expectations
        time -= offset;

        Log("Time: {0}, Expected: {1}-{2}", time, TimeSpan.FromMinutes(59), TimeSpan.FromMinutes(60));
        (time >= TimeSpan.FromMinutes(59)).Should().BeTrue();
        (time <= TimeSpan.FromMinutes(60.1)).Should().BeTrue();
        (await c).Should().BeNull();

        timeResult = await d;
        Assert.NotNull(timeResult);
        time = timeResult.Value;

        (time >= TimeSpan.FromMinutes(89)).Should().BeTrue();
        (time <= TimeSpan.FromMinutes(90.1)).Should().BeTrue();
        (await e).Should().BeNull();
        (await f).Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task key_expiry_time(bool disablePTimes)
    {
        await using var conn = Create(disabledCommands: GetMap(disablePTimes), require: RedisFeatures.v7_0_0_rc1);

        var key = Me();
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        var expireTime = DateTime.UtcNow.AddHours(1);
        db.StringSet(key, "new value", flags: CommandFlags.FireAndForget);
        db.KeyExpire(key, expireTime, CommandFlags.FireAndForget);

        var time = db.KeyExpireTime(key);
        Assert.NotNull(time);
        time!.Value.Should().BeCloseTo(expireTime, TimeSpan.FromSeconds(30));

        // Without associated expiration time
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.StringSet(key, "new value", flags: CommandFlags.FireAndForget);
        time = db.KeyExpireTime(key);
        time.Should().BeNull();

        // Non existing key
        db.KeyDelete(key, CommandFlags.FireAndForget);
        time = db.KeyExpireTime(key);
        time.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task key_expiry_time_async(bool disablePTimes)
    {
        await using var conn = Create(disabledCommands: GetMap(disablePTimes), require: RedisFeatures.v7_0_0_rc1);

        var key = Me();
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        var expireTime = DateTime.UtcNow.AddHours(1);
        db.StringSet(key, "new value", flags: CommandFlags.FireAndForget);
        db.KeyExpire(key, expireTime, CommandFlags.FireAndForget);

        var time = await db.KeyExpireTimeAsync(key);
        Assert.NotNull(time);
        time.Value.Should().BeCloseTo(expireTime, TimeSpan.FromSeconds(30));

        // Without associated expiration time
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.StringSet(key, "new value", flags: CommandFlags.FireAndForget);
        time = await db.KeyExpireTimeAsync(key);
        time.Should().BeNull();

        // Non existing key
        db.KeyDelete(key, CommandFlags.FireAndForget);
        time = await db.KeyExpireTimeAsync(key);
        time.Should().BeNull();
    }
}
