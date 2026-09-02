using System;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[RunPerProtocol]
public class IncrexIntegrationTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    [Fact(Timeout = 5000)]
    public async Task string_increment_increx_int64_with_bounds_and_expiry()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v8_8_0);
        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.StringSet(key, 10);

        //Act
        var result = await db.StringIncrementAsync(key, 2L, TimeSpan.FromSeconds(5), lowerBound: 0, upperBound: 20).WaitAsync(TestContext.Current.CancellationToken);

        //Assert
        result.Value.Should().Be(12);
        result.AppliedIncrement.Should().Be(2);
        ((long)db.StringGet(key)).Should().Be(12);
        ((await db.KeyTimeToLiveAsync(key)) > TimeSpan.Zero).Should().BeTrue();
    }

    [Fact(Timeout = 5000)]
    public async Task string_increment_increx_double_with_absolute_expiry_and_enx()
    {
        await using var conn = Create(require: RedisFeatures.v8_8_0);
        var db = conn.GetDatabase();
        var key = Me();
        var when = DateTime.UtcNow.AddMinutes(30).AddMilliseconds(14);
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.StringSet(key, 3.25, TimeSpan.FromMinutes(10));
        var beforeTtl = await db.KeyTimeToLiveAsync(key);

        var result = await db.StringIncrementAsync(key, 1.25, new Expiration(when, ExpirationFlags.ExpireIfNotExists), lowerBound: -1.5, upperBound: 9.5).WaitAsync(TestContext.Current.CancellationToken);

        result.Value.Should().Be(4.5);
        result.AppliedIncrement.Should().Be(1.25);
        ((double)db.StringGet(key)).Should().Be(4.5);
        var afterTtl = await db.KeyTimeToLiveAsync(key);
        beforeTtl.Should().NotBeNull();
        afterTtl.Should().NotBeNull();
        (afterTtl <= beforeTtl).Should().BeTrue();
        (afterTtl > TimeSpan.FromMinutes(8)).Should().BeTrue();
    }

    [Fact(Timeout = 5000)]
    public async Task string_increment_increx_sync_version_parses_result()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v8_8_0);
        var db = conn.GetDatabase();
        var intKey = (RedisKey)(Me() + ":int");
        var doubleKey = (RedisKey)(Me() + ":double");
        db.KeyDelete([intKey, doubleKey], CommandFlags.FireAndForget);
        var intResult = db.StringIncrement(intKey, 3L, Expiration.Default);

        //the synchronous API this test exists to cover takes no CancellationToken, so the test's own
        //token is observed between the two blocking calls - which is what [Fact(Timeout)] needs (xUnit1069)
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        //Act
        var doubleResult = db.StringIncrement(doubleKey, 1.5, Expiration.Default);

        //Assert
        intResult.Value.Should().Be(3);
        intResult.AppliedIncrement.Should().Be(3);
        doubleResult.Value.Should().Be(1.5);
        doubleResult.AppliedIncrement.Should().Be(1.5);
    }

    [Fact(Timeout = 5000)]
    public async Task string_increment_increx_default_rejects_when_bound_exceeded()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v8_8_0);
        var db = conn.GetDatabase();
        var intKey = (RedisKey)(Me() + ":int");
        var doubleKey = (RedisKey)(Me() + ":double");
        db.KeyDelete([intKey, doubleKey], CommandFlags.FireAndForget);
        db.StringSet(intKey, 5);
        db.StringSet(doubleKey, 5.5);
        var intResult = await db.StringIncrementAsync(intKey, 1L, TimeSpan.FromSeconds(5), lowerBound: 10).WaitAsync(TestContext.Current.CancellationToken);

        //Act
        var doubleResult = await db.StringIncrementAsync(doubleKey, 1.25, TimeSpan.FromSeconds(5), lowerBound: 10.25).WaitAsync(TestContext.Current.CancellationToken);

        //Assert
        intResult.Value.Should().Be(5);
        intResult.AppliedIncrement.Should().Be(0);
        ((long)db.StringGet(intKey)).Should().Be(5);
        (await db.KeyTimeToLiveAsync(intKey)).Should().BeNull();
        doubleResult.Value.Should().Be(5.5);
        doubleResult.AppliedIncrement.Should().Be(0);
        ((double)db.StringGet(doubleKey)).Should().Be(5.5);
        (await db.KeyTimeToLiveAsync(doubleKey)).Should().BeNull();
    }

    [Fact(Timeout = 5000)]
    public async Task string_increment_increx_saturate_clamps_to_bound()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v8_8_0);
        var db = conn.GetDatabase();
        var intKey = (RedisKey)(Me() + ":int");
        var doubleKey = (RedisKey)(Me() + ":double");
        db.KeyDelete([intKey, doubleKey], CommandFlags.FireAndForget);
        db.StringSet(intKey, 8);
        db.StringSet(doubleKey, 8.25);
        var intResult = await db.StringIncrementAsync(intKey, 5L, TimeSpan.FromSeconds(5), upperBound: 10, options: IncrementOptions.Saturate).WaitAsync(TestContext.Current.CancellationToken);

        //Act
        var doubleResult = await db.StringIncrementAsync(doubleKey, 5.5, TimeSpan.FromSeconds(5), upperBound: 10.5, options: IncrementOptions.Saturate).WaitAsync(TestContext.Current.CancellationToken);

        //Assert
        intResult.Value.Should().Be(10);
        intResult.AppliedIncrement.Should().Be(2);
        ((long)db.StringGet(intKey)).Should().Be(10);
        ((await db.KeyTimeToLiveAsync(intKey)) > TimeSpan.Zero).Should().BeTrue();
        doubleResult.Value.Should().Be(10.5);
        doubleResult.AppliedIncrement.Should().Be(2.25);
        ((double)db.StringGet(doubleKey)).Should().Be(10.5);
        ((await db.KeyTimeToLiveAsync(doubleKey)) > TimeSpan.Zero).Should().BeTrue();
    }

    [Fact(Timeout = 5000)]
    public async Task string_increment_increx_default_retains_existing_ttl()
    {
        await using var conn = Create(require: RedisFeatures.v8_8_0);
        var db = conn.GetDatabase();
        var intKey = (RedisKey)(Me() + ":int");
        var doubleKey = (RedisKey)(Me() + ":double");
        db.KeyDelete([intKey, doubleKey], CommandFlags.FireAndForget);
        db.StringSet(intKey, 5, TimeSpan.FromMinutes(5));
        db.StringSet(doubleKey, 5.5, TimeSpan.FromMinutes(5));
        var beforeIntTtl = await db.KeyTimeToLiveAsync(intKey);
        var beforeDoubleTtl = await db.KeyTimeToLiveAsync(doubleKey);

        var intResult = await db.StringIncrementAsync(intKey, 2L, Expiration.Default).WaitAsync(TestContext.Current.CancellationToken);
        var doubleResult = await db.StringIncrementAsync(doubleKey, 2.25, Expiration.Default).WaitAsync(TestContext.Current.CancellationToken);

        intResult.Value.Should().Be(7);
        intResult.AppliedIncrement.Should().Be(2);
        var afterIntTtl = await db.KeyTimeToLiveAsync(intKey);
        beforeIntTtl.Should().NotBeNull();
        afterIntTtl.Should().NotBeNull();
        (afterIntTtl <= beforeIntTtl).Should().BeTrue();
        (afterIntTtl > TimeSpan.FromMinutes(4)).Should().BeTrue();

        doubleResult.Value.Should().Be(7.75);
        doubleResult.AppliedIncrement.Should().Be(2.25);
        var afterDoubleTtl = await db.KeyTimeToLiveAsync(doubleKey);
        beforeDoubleTtl.Should().NotBeNull();
        afterDoubleTtl.Should().NotBeNull();
        (afterDoubleTtl <= beforeDoubleTtl).Should().BeTrue();
        (afterDoubleTtl > TimeSpan.FromMinutes(4)).Should().BeTrue();
    }

    [Theory(Timeout = 5000)]
    [InlineData(5L, 2L, null, 10L, IncrementOptions.None, 7L, 2L, true)]
    [InlineData(5L, 1L, 10L, null, IncrementOptions.None, 5L, 0L, false)]
    [InlineData(5L, 2L, null, 10L, IncrementOptions.Saturate, 7L, 2L, true)]
    [InlineData(8L, 5L, null, 10L, IncrementOptions.Saturate, 10L, 2L, true)]
    // [InlineData(10L, 5L, null, 10L, IncrementOptions.Saturate, 10L, 0L, false)]
    [InlineData(10L, 5L, null, 10L, IncrementOptions.Saturate, 10L, 0L, true)]
    // [InlineData(11L, 1L, null, 10L, IncrementOptions.Saturate, 11L, 0L, false)]
    [InlineData(11L, 1L, null, 10L, IncrementOptions.Saturate, 10L, -1L, true)]
    public async Task string_increment_increx_int64_expiration_side_effects(
        long initialValue,
        long increment,
        long? lowerBound,
        long? upperBound,
        IncrementOptions options,
        long expectedValue,
        long expectedAppliedIncrement,
        bool expectExpiryChanged)
    {
        await using var conn = Create(require: RedisFeatures.v8_8_0);
        var db = conn.GetDatabase();
        var key = (RedisKey)Me();

        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.StringSet(key, initialValue, ExistingExpiry);
        var beforeTtl = await db.KeyTimeToLiveAsync(key);

        var result = await db.StringIncrementAsync(key, increment, NewExpiry, lowerBound, upperBound, options).WaitAsync(TestContext.Current.CancellationToken);

        result.Value.Should().Be(expectedValue);
        result.AppliedIncrement.Should().Be(expectedAppliedIncrement);
        ((long)db.StringGet(key)).Should().Be(expectedValue);
        await AssertExpiryAsync(db, key, beforeTtl, expectExpiryChanged);
    }

    [Theory(Timeout = 5000)]
    [InlineData(5.5, 1.25, null, 10.5, IncrementOptions.None, 6.75, 1.25, true)]
    [InlineData(5.5, 1.25, 10.25, null, IncrementOptions.None, 5.5, 0D, false)]
    [InlineData(5.5, 1.25, null, 10.5, IncrementOptions.Saturate, 6.75, 1.25, true)]
    [InlineData(8.25, 5.5, null, 10.5, IncrementOptions.Saturate, 10.5, 2.25, true)]
    // [InlineData(10.5, 5.5, null, 10.5, IncrementOptions.Saturate, 10.5, 0D, false)]
    [InlineData(10.5, 5.5, null, 10.5, IncrementOptions.Saturate, 10.5, 0D, true)]
    // [InlineData(11.5, 1.25, null, 10.5, IncrementOptions.Saturate, 11.5, 0D, false)]
    [InlineData(11.5, 1.25, null, 10.5, IncrementOptions.Saturate, 10.5, -1D, true)]
    public async Task string_increment_increx_double_expiration_side_effects(
        double initialValue,
        double increment,
        double? lowerBound,
        double? upperBound,
        IncrementOptions options,
        double expectedValue,
        double expectedAppliedIncrement,
        bool expectExpiryChanged)
    {
        await using var conn = Create(require: RedisFeatures.v8_8_0);
        var db = conn.GetDatabase();
        var key = (RedisKey)Me();

        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.StringSet(key, initialValue, ExistingExpiry);
        var beforeTtl = await db.KeyTimeToLiveAsync(key);

        var result = await db.StringIncrementAsync(key, increment, NewExpiry, lowerBound, upperBound, options).WaitAsync(TestContext.Current.CancellationToken);

        result.Value.Should().Be(expectedValue);
        result.AppliedIncrement.Should().Be(expectedAppliedIncrement);
        ((double)db.StringGet(key)).Should().Be(expectedValue);
        await AssertExpiryAsync(db, key, beforeTtl, expectExpiryChanged);
    }

    private static async Task AssertExpiryAsync(IDatabase db, RedisKey key, TimeSpan? beforeTtl, bool expectExpiryChanged)
    {
        var afterTtl = await db.KeyTimeToLiveAsync(key);
        beforeTtl.Should().NotBeNull();
        afterTtl.Should().NotBeNull();

        if (expectExpiryChanged)
        {
            (afterTtl <= ChangedExpiryUpperBound).Should().BeTrue($"Expected {key} TTL to use the new expiry, but was {afterTtl}.");
            (afterTtl > TimeSpan.Zero).Should().BeTrue($"Expected {key} TTL to be positive, but was {afterTtl}.");
        }
        else
        {
            (afterTtl > UnchangedExpiryLowerBound).Should().BeTrue($"Expected {key} TTL to retain the original expiry, but was {afterTtl}.");
            (afterTtl <= beforeTtl).Should().BeTrue($"Expected {key} TTL not to grow, but went from {beforeTtl} to {afterTtl}.");
        }
    }

    private static readonly TimeSpan ExistingExpiry = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan NewExpiry = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ChangedExpiryUpperBound = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan UnchangedExpiryLowerBound = TimeSpan.FromMinutes(10);
}
