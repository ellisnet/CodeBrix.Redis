using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class IncrexUnitTests(ITestOutputHelper log)
{
    private RedisKey Me([CallerMemberName] string callerName = "") => callerName;

    [Fact]
    public async Task string_increment_increx_int64_with_bounds_and_expiry()
    {
        using var server = new IncrexTestServer(log);
        await using var muxer = await server.ConnectAsync();
        var db = muxer.GetDatabase();
        var key = Me();

        db.StringSet(key, 10);

        var result = await db.StringIncrementAsync(key, 2L, TimeSpan.FromSeconds(5), lowerBound: 0, upperBound: 20);

        result.Value.Should().Be(12);
        result.AppliedIncrement.Should().Be(2);
        ((long)db.StringGet(key)).Should().Be(12);
        ((await db.KeyTimeToLiveAsync(key)) > TimeSpan.Zero).Should().BeTrue();

        var request = server.LastRequest!;
        request.Key.Should().Be(key);
        request.IsFloat.Should().BeFalse();
        request.Increment.Should().Be("2");
        request.LowerBound.Should().Be("0");
        request.UpperBound.Should().Be("20");
        request.Saturate.Should().BeFalse();
        request.ExpiryMode.Should().Be("EX");
        request.ExpiryValue.Should().Be("5");
        request.Enx.Should().BeFalse();
    }

    [Fact]
    public async Task string_increment_increx_double_with_absolute_expiry_and_enx()
    {
        using var server = new IncrexTestServer(log);
        await using var muxer = await server.ConnectAsync();
        var db = muxer.GetDatabase();
        var key = Me();
        var when = new DateTime(2025, 7, 23, 10, 4, 14, DateTimeKind.Utc).AddMilliseconds(14);
        db.StringSet(key, 3.25, TimeSpan.FromMinutes(10));
        var beforeTtl = await db.KeyTimeToLiveAsync(key);

        var result = await db.StringIncrementAsync(key, 1.25, new Expiration(when, ExpirationFlags.ExpireIfNotExists), lowerBound: -1.5, upperBound: 9.5);

        result.Value.Should().Be(4.5);
        result.AppliedIncrement.Should().Be(1.25);
        ((double)db.StringGet(key)).Should().Be(4.5);
        var afterTtl = await db.KeyTimeToLiveAsync(key);
        beforeTtl.Should().NotBeNull();
        afterTtl.Should().NotBeNull();
        (afterTtl <= beforeTtl).Should().BeTrue();
        (afterTtl > TimeSpan.FromMinutes(8)).Should().BeTrue();

        var request = server.LastRequest!;
        request.Key.Should().Be(key);
        request.IsFloat.Should().BeTrue();
        request.Increment.Should().Be("1.25");
        request.LowerBound.Should().Be("-1.5");
        request.UpperBound.Should().Be("9.5");
        request.Saturate.Should().BeFalse();
        request.ExpiryMode.Should().Be("PXAT");
        request.ExpiryValue.Should().Be("1753265054014");
        request.Enx.Should().BeTrue();
    }

    [Fact]
    [RunPerProtocol]
    public async Task string_increment_increx_execute_uses_number_result_types()
    {
        using var server = new IncrexTestServer(log);
        await using var muxer = await server.ConnectAsync();
        var db = muxer.GetDatabase();
        var key = nameof(string_increment_increx_execute_uses_number_result_types);
        var expectedFractionalType = TestContext.Current.IsResp3() ? ResultType.Double : ResultType.BulkString;

        var fractional = await db.ExecuteAsync("INCREX", (RedisKey)(key + ":fractional"), "BYFLOAT", 1.5);
        var fractionalItems = (RedisResult[])fractional!;
        fractionalItems.Length.Should().Be(2);
        fractionalItems[0].Resp3Type.Should().Be(expectedFractionalType);
        fractionalItems[1].Resp3Type.Should().Be(expectedFractionalType);
        ((double)fractionalItems[0]).Should().Be(1.5);
        ((double)fractionalItems[1]).Should().Be(1.5);

        var integral = await db.ExecuteAsync("INCREX", (RedisKey)(key + ":integral"), "BYFLOAT", 2.0);
        var integralItems = (RedisResult[])integral!;
        integralItems.Length.Should().Be(2);
        integralItems[0].Resp3Type.Should().Be(ResultType.Integer);
        integralItems[1].Resp3Type.Should().Be(ResultType.Integer);
        ((long)integralItems[0]).Should().Be(2);
        ((long)integralItems[1]).Should().Be(2);
    }

    [Fact]
    public async Task string_increment_increx_sync_version_parses_result()
    {
        //Arrange
        using var server = new IncrexTestServer(log);
        await using var muxer = await server.ConnectAsync();
        var db = muxer.GetDatabase();

        //Act
        var result = db.StringIncrement(Me(), 3L, Expiration.Default);

        //Assert
        result.Value.Should().Be(3);
        result.AppliedIncrement.Should().Be(3);
    }

    [Fact]
    public async Task string_increment_increx_default_rejects_when_bound_exceeded()
    {
        //Arrange
        using var server = new IncrexTestServer(log);
        await using var muxer = await server.ConnectAsync();
        var db = muxer.GetDatabase();
        var key = Me();
        db.StringSet(key, 5);

        //Act
        var result = await db.StringIncrementAsync(key, 1L, TimeSpan.FromSeconds(5), lowerBound: 10);

        //Assert
        result.Value.Should().Be(5);
        result.AppliedIncrement.Should().Be(0);
        ((long)db.StringGet(key)).Should().Be(5);
        (await db.KeyTimeToLiveAsync(key)).Should().BeNull();
        server.LastRequest!.Saturate.Should().BeFalse();
    }

    [Fact]
    public async Task string_increment_increx_invalid_options_throw()
    {
        //Arrange
        using var server = new IncrexTestServer(log);
        await using var muxer = await server.ConnectAsync();
        var db = muxer.GetDatabase();

        //Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => db.StringIncrement(Me(), 1L, TimeSpan.FromSeconds(5), options: (IncrementOptions)2));

        //Assert
        ex.ParamName.Should().Be("options");
    }

    [Fact]
    public async Task string_increment_increx_saturate_clamps_to_bound()
    {
        //Arrange
        using var server = new IncrexTestServer(log);
        await using var muxer = await server.ConnectAsync();
        var db = muxer.GetDatabase();
        var key = Me();
        db.StringSet(key, 8);

        //Act
        var result = await db.StringIncrementAsync(key, 5L, TimeSpan.FromSeconds(5), upperBound: 10, options: IncrementOptions.Saturate);

        //Assert
        result.Value.Should().Be(10);
        result.AppliedIncrement.Should().Be(2);
        ((long)db.StringGet(key)).Should().Be(10);
        ((await db.KeyTimeToLiveAsync(key)) > TimeSpan.Zero).Should().BeTrue();
        server.LastRequest!.Saturate.Should().BeTrue();
    }

    [Fact]
    public async Task string_increment_increx_default_retains_existing_ttl()
    {
        using var server = new IncrexTestServer(log);
        await using var muxer = await server.ConnectAsync();
        var db = muxer.GetDatabase();
        var key = Me();
        db.StringSet(key, 5, TimeSpan.FromMinutes(5));
        var beforeTtl = await db.KeyTimeToLiveAsync(key);

        var result = await db.StringIncrementAsync(key, 2L, Expiration.Default);

        result.Value.Should().Be(7);
        result.AppliedIncrement.Should().Be(2);
        var afterTtl = await db.KeyTimeToLiveAsync(key);
        beforeTtl.Should().NotBeNull();
        afterTtl.Should().NotBeNull();
        (afterTtl <= beforeTtl).Should().BeTrue();
        (afterTtl > TimeSpan.FromMinutes(4)).Should().BeTrue();
    }

    [Theory]
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
        using var server = new IncrexTestServer(log);
        await using var muxer = await server.ConnectAsync();
        var db = muxer.GetDatabase();
        var key = Me();

        db.StringSet(key, initialValue, ExistingExpiry);
        var beforeTtl = await db.KeyTimeToLiveAsync(key);

        var result = await db.StringIncrementAsync(key, increment, NewExpiry, lowerBound, upperBound, options);

        result.Value.Should().Be(expectedValue);
        result.AppliedIncrement.Should().Be(expectedAppliedIncrement);
        ((long)db.StringGet(key)).Should().Be(expectedValue);
        await AssertExpiryAsync(db, key, beforeTtl, expectExpiryChanged);
    }

    [Theory]
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
        using var server = new IncrexTestServer(log);
        await using var muxer = await server.ConnectAsync();
        var db = muxer.GetDatabase();
        var key = Me();

        db.StringSet(key, initialValue, ExistingExpiry);
        var beforeTtl = await db.KeyTimeToLiveAsync(key);

        var result = await db.StringIncrementAsync(key, increment, NewExpiry, lowerBound, upperBound, options);

        result.Value.Should().Be(expectedValue);
        result.AppliedIncrement.Should().Be(expectedAppliedIncrement);
        ((double)db.StringGet(key)).Should().Be(expectedValue);
        await AssertExpiryAsync(db, key, beforeTtl, expectExpiryChanged);
    }

    [Fact]
    public async Task string_increment_increx_rejects_keep_ttl()
    {
        //Arrange
        using var server = new IncrexTestServer(log);
        await using var muxer = await server.ConnectAsync();
        var db = muxer.GetDatabase();

        //Act
        var ex = Assert.Throws<ArgumentException>(() => db.StringIncrement(Me(), 1L, Expiration.KeepTtl));

        //Assert
        ex.ParamName.Should().Be("expiry");
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
