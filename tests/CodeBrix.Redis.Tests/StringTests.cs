using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

/// <summary>
/// Tests for <see href="https://redis.io/commands#string"/>.
/// </summary>
[RunPerProtocol]
public class StringTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    [Fact]
    public async Task append()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        var server = GetServer(conn);
        var key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        var l0 = server.Features.StringLength ? db.StringLengthAsync(key) : null;

        var s0 = db.StringGetAsync(key);

        db.StringSet(key, "abc", flags: CommandFlags.FireAndForget);
        var s1 = db.StringGetAsync(key);
        var l1 = server.Features.StringLength ? db.StringLengthAsync(key) : null;

        var result = db.StringAppendAsync(key, Encode("defgh"));
        var s3 = db.StringGetAsync(key);
        var l2 = server.Features.StringLength ? db.StringLengthAsync(key) : null;

        ((string?)await s0).Should().BeNull();
        (await s1).Should().Be("abc");
        (await result).Should().Be(8);
        (await s3).Should().Be("abcdefgh");

        if (server.Features.StringLength)
        {
            (await l0!).Should().Be(0);
            (await l1!).Should().Be(3);
            (await l2!).Should().Be(8);
        }
    }

    [Fact]
    public async Task set()
    {
        //Arrange
        await using var conn = Create();

        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        db.StringSet(key, "abc", flags: CommandFlags.FireAndForget);
        var v1 = db.StringGetAsync(key);

        db.StringSet(key, Encode("def"), flags: CommandFlags.FireAndForget);

        //Act
        var v2 = db.StringGetAsync(key);

        //Assert
        (await v1).Should().Be("abc");
        Decode(await v2).Should().Be("def");
    }

    [Fact]
    public async Task set_empty()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        db.StringSet(key, new byte[] { });
        var exists = await db.KeyExistsAsync(key);
        var val = await db.StringGetAsync(key);

        exists.Should().BeTrue();
        Log("Value: " + val);
        val.Length().Should().Be(0);
    }

    [Fact]
    public async Task string_get_set_expiry_no_value()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        //Act
        var emptyVal = await db.StringGetSetExpiryAsync(key, TimeSpan.FromHours(1));

        //Assert
        emptyVal.Should().Be(RedisValue.Null);
    }

    [Fact]
    public async Task string_get_set_expiry_relative()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        db.StringSet(key, "abc", TimeSpan.FromHours(1));
        var relativeSec = db.StringGetSetExpiryAsync(key, TimeSpan.FromMinutes(30));
        var relativeSecTtl = db.KeyTimeToLiveAsync(key);

        (await relativeSec).Should().Be("abc");
        var time = await relativeSecTtl;
        Assert.NotNull(time);
        //kept as xUnit: SimpleTimeSpanAssertions has no BeInRange, and splitting it into two
        //comparisons would assert something subtly different on the boundaries.
        Assert.InRange(time.Value, TimeSpan.FromMinutes(29.8), TimeSpan.FromMinutes(30.2));
    }

    [Fact]
    public async Task string_get_set_expiry_absolute()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        db.StringSet(key, "abc", TimeSpan.FromHours(1));
        var newDate = DateTime.UtcNow.AddMinutes(30);
        var val = db.StringGetSetExpiryAsync(key, newDate);
        var valTtl = db.KeyTimeToLiveAsync(key);

        (await val).Should().Be("abc");
        var time = await valTtl;
        Assert.NotNull(time);
        //kept as xUnit: SimpleTimeSpanAssertions has no BeInRange, and splitting it into two
        //comparisons would assert something subtly different on the boundaries.
        Assert.InRange(time.Value, TimeSpan.FromMinutes(29.8), TimeSpan.FromMinutes(30.2));

        // And ensure our type checking works
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => db.StringGetSetExpiryAsync(key, new DateTime(100, DateTimeKind.Unspecified)));
        Assert.NotNull(ex);
    }

    [Fact]
    public async Task string_get_set_expiry_persist()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        db.StringSet(key, "abc", TimeSpan.FromHours(1));
        var val = db.StringGetSetExpiryAsync(key, null);

        //Act
        var valTtl = db.KeyTimeToLiveAsync(key);

        //Assert
        (await val).Should().Be("abc");
        (await valTtl).Should().BeNull();
    }

    [Fact]
    public async Task get_lease()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        db.StringSet(key, "abc", flags: CommandFlags.FireAndForget);
        using (var v1 = await db.StringGetLeaseAsync(key).ConfigureAwait(false))
        {
            string? s = v1?.DecodeString();
            s.Should().Be("abc");
        }
    }

    [Fact]
    public async Task get_lease_as_stream()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        db.StringSet(key, "abc", flags: CommandFlags.FireAndForget);
        var lease = await db.StringGetLeaseAsync(key).ConfigureAwait(false);
        Assert.NotNull(lease);
        using (var v1 = lease.AsStream())
        {
            using (var sr = new StreamReader(v1))
            {
                string s = sr.ReadToEnd();
                s.Should().Be("abc");
            }
        }
    }

    [Fact]
    public async Task get_delete()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var prefix = Me();
        db.KeyDelete(prefix + "1", CommandFlags.FireAndForget);
        db.KeyDelete(prefix + "2", CommandFlags.FireAndForget);
        db.StringSet(prefix + "1", "abc", flags: CommandFlags.FireAndForget);

        db.KeyExists(prefix + "1").Should().BeTrue();
        db.KeyExists(prefix + "2").Should().BeFalse();

        var s0 = db.StringGetDelete(prefix + "1");
        var s2 = db.StringGetDelete(prefix + "2");

        db.KeyExists(prefix + "1").Should().BeFalse();
        s0.Should().Be("abc");
        s2.Should().Be(RedisValue.Null);
    }

    [Fact]
    public async Task get_delete_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var prefix = Me();
        db.KeyDelete(prefix + "1", CommandFlags.FireAndForget);
        db.KeyDelete(prefix + "2", CommandFlags.FireAndForget);
        db.StringSet(prefix + "1", "abc", flags: CommandFlags.FireAndForget);

        db.KeyExists(prefix + "1").Should().BeTrue();
        db.KeyExists(prefix + "2").Should().BeFalse();

        var s0 = db.StringGetDeleteAsync(prefix + "1");
        var s2 = db.StringGetDeleteAsync(prefix + "2");

        db.KeyExists(prefix + "1").Should().BeFalse();
        (await s0).Should().Be("abc");
        (await s2).Should().Be(RedisValue.Null);
    }

    [Fact]
    public async Task set_not_exists()
    {
        //Arrange
        await using var conn = Create();

        var db = conn.GetDatabase();
        var prefix = Me();
        db.KeyDelete(prefix + "1", CommandFlags.FireAndForget);
        db.KeyDelete(prefix + "2", CommandFlags.FireAndForget);
        db.KeyDelete(prefix + "3", CommandFlags.FireAndForget);
        db.KeyDelete(prefix + "4", CommandFlags.FireAndForget);
        db.KeyDelete(prefix + "5", CommandFlags.FireAndForget);
        db.StringSet(prefix + "1", "abc", flags: CommandFlags.FireAndForget);

        var x0 = db.StringSetAsync(prefix + "1", "def", when: When.NotExists);
        var x1 = db.StringSetAsync(prefix + "1", Encode("def"), when: When.NotExists);
        var x2 = db.StringSetAsync(prefix + "2", "def", when: When.NotExists);
        var x3 = db.StringSetAsync(prefix + "3", Encode("def"), when: When.NotExists);
        var x4 = db.StringSetAsync(prefix + "4", "def", expiry: TimeSpan.FromSeconds(4), when: When.NotExists);
        var x5 = db.StringSetAsync(prefix + "5", "def", expiry: TimeSpan.FromMilliseconds(4001), when: When.NotExists);

        var s0 = db.StringGetAsync(prefix + "1");
        var s2 = db.StringGetAsync(prefix + "2");

        //Act
        var s3 = db.StringGetAsync(prefix + "3");

        //Assert
        (await x0).Should().BeFalse();
        (await x1).Should().BeFalse();
        (await x2).Should().BeTrue();
        (await x3).Should().BeTrue();
        (await x4).Should().BeTrue();
        (await x5).Should().BeTrue();
        (await s0).Should().Be("abc");
        (await s2).Should().Be("def");
        (await s3).Should().Be("def");
    }

    [Fact]
    public async Task set_keep_ttl()
    {
        await using var conn = Create(require: RedisFeatures.v6_0_0);

        var db = conn.GetDatabase();
        var prefix = Me();
        db.KeyDelete(prefix + "1", CommandFlags.FireAndForget);
        db.KeyDelete(prefix + "2", CommandFlags.FireAndForget);
        db.KeyDelete(prefix + "3", CommandFlags.FireAndForget);
        db.StringSet(prefix + "1", "abc", flags: CommandFlags.FireAndForget);
        db.StringSet(prefix + "2", "abc", expiry: TimeSpan.FromMinutes(5), flags: CommandFlags.FireAndForget);
        db.StringSet(prefix + "3", "abc", expiry: TimeSpan.FromMinutes(10), flags: CommandFlags.FireAndForget);

        var x0 = db.KeyTimeToLiveAsync(prefix + "1");
        var x1 = db.KeyTimeToLiveAsync(prefix + "2");
        var x2 = db.KeyTimeToLiveAsync(prefix + "3");

        (await x0).Should().BeNull();
        (await x1 > TimeSpan.FromMinutes(4)).Should().BeTrue("Over 4");
        (await x1 <= TimeSpan.FromMinutes(5)).Should().BeTrue("Under 5");
        (await x2 > TimeSpan.FromMinutes(9)).Should().BeTrue("Over 9");
        (await x2 <= TimeSpan.FromMinutes(10)).Should().BeTrue("Under 10");

        db.StringSet(prefix + "1", "def", Expiration.KeepTtl, flags: CommandFlags.FireAndForget);
        db.StringSet(prefix + "2", "def", flags: CommandFlags.FireAndForget);
        db.StringSet(prefix + "3", "def", Expiration.KeepTtl, flags: CommandFlags.FireAndForget);

        var y0 = db.KeyTimeToLiveAsync(prefix + "1");
        var y1 = db.KeyTimeToLiveAsync(prefix + "2");
        var y2 = db.KeyTimeToLiveAsync(prefix + "3");

        (await y0).Should().BeNull();
        (await y1).Should().BeNull();
        (await y2 > TimeSpan.FromMinutes(9)).Should().BeTrue("Over 9");
        (await y2 <= TimeSpan.FromMinutes(10)).Should().BeTrue("Under 10");
    }

    [Fact]
    public async Task set_and_get()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var prefix = Me();
        db.KeyDelete(prefix + "1", CommandFlags.FireAndForget);
        db.KeyDelete(prefix + "2", CommandFlags.FireAndForget);
        db.KeyDelete(prefix + "3", CommandFlags.FireAndForget);
        db.KeyDelete(prefix + "4", CommandFlags.FireAndForget);
        db.KeyDelete(prefix + "5", CommandFlags.FireAndForget);
        db.KeyDelete(prefix + "6", CommandFlags.FireAndForget);
        db.KeyDelete(prefix + "7", CommandFlags.FireAndForget);
        db.KeyDelete(prefix + "8", CommandFlags.FireAndForget);
        db.KeyDelete(prefix + "9", CommandFlags.FireAndForget);
        db.KeyDelete(prefix + "10", CommandFlags.FireAndForget);
        db.StringSet(prefix + "1", "abc", flags: CommandFlags.FireAndForget);
        db.StringSet(prefix + "2", "abc", flags: CommandFlags.FireAndForget);
        db.StringSet(prefix + "4", "abc", flags: CommandFlags.FireAndForget);
        db.StringSet(prefix + "6", "abc", flags: CommandFlags.FireAndForget);
        db.StringSet(prefix + "7", "abc", flags: CommandFlags.FireAndForget);
        db.StringSet(prefix + "8", "abc", flags: CommandFlags.FireAndForget);
        db.StringSet(prefix + "9", "abc", flags: CommandFlags.FireAndForget);
        db.StringSet(prefix + "10", "abc", expiry: TimeSpan.FromMinutes(10), flags: CommandFlags.FireAndForget);

        var x0 = db.StringSetAndGetAsync(prefix + "1", RedisValue.Null);
        var x1 = db.StringSetAndGetAsync(prefix + "2", "def");
        var x2 = db.StringSetAndGetAsync(prefix + "3", "def");
        var x3 = db.StringSetAndGetAsync(prefix + "4", "def", when: When.Exists);
        var x4 = db.StringSetAndGetAsync(prefix + "5", "def", when: When.Exists);
        var x5 = db.StringSetAndGetAsync(prefix + "6", "def", expiry: TimeSpan.FromSeconds(4));
        var x6 = db.StringSetAndGetAsync(prefix + "7", "def", expiry: TimeSpan.FromMilliseconds(4001));
        var x7 = db.StringSetAndGetAsync(prefix + "8", "def", expiry: TimeSpan.FromSeconds(4), when: When.Exists);
        var x8 = db.StringSetAndGetAsync(prefix + "9", "def", expiry: TimeSpan.FromMilliseconds(4001), when: When.Exists);

        var y0 = db.StringSetAndGetAsync(prefix + "10", "def", keepTtl: true);
        var y1 = db.KeyTimeToLiveAsync(prefix + "10");
        var y2 = db.StringGetAsync(prefix + "10");

        var s0 = db.StringGetAsync(prefix + "1");
        var s1 = db.StringGetAsync(prefix + "2");
        var s2 = db.StringGetAsync(prefix + "3");
        var s3 = db.StringGetAsync(prefix + "4");

        //Act
        var s4 = db.StringGetAsync(prefix + "5");

        //Assert
        (await x0).Should().Be("abc");
        (await x1).Should().Be("abc");
        (await x2).Should().Be(RedisValue.Null);
        (await x3).Should().Be("abc");
        (await x4).Should().Be(RedisValue.Null);
        (await x5).Should().Be("abc");
        (await x6).Should().Be("abc");
        (await x7).Should().Be("abc");
        (await x8).Should().Be("abc");

        (await y0).Should().Be("abc");
        (await y1 <= TimeSpan.FromMinutes(10)).Should().BeTrue("Under 10 min");
        (await y1 >= TimeSpan.FromMinutes(8)).Should().BeTrue("Over 8 min");
        (await y2).Should().Be("def");

        (await s0).Should().Be(RedisValue.Null);
        (await s1).Should().Be("def");
        (await s2).Should().Be("def");
        (await s3).Should().Be("def");
        (await s4).Should().Be(RedisValue.Null);
    }

    [Fact]
    public async Task set_not_exists_and_get()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var db = conn.GetDatabase();
        var prefix = Me();
        db.KeyDelete(prefix + "1", CommandFlags.FireAndForget);
        db.KeyDelete(prefix + "2", CommandFlags.FireAndForget);
        db.KeyDelete(prefix + "3", CommandFlags.FireAndForget);
        db.KeyDelete(prefix + "4", CommandFlags.FireAndForget);
        db.StringSet(prefix + "1", "abc", flags: CommandFlags.FireAndForget);

        var x0 = db.StringSetAndGetAsync(prefix + "1", "def", when: When.NotExists);
        var x1 = db.StringSetAndGetAsync(prefix + "2", "def", when: When.NotExists);
        var x2 = db.StringSetAndGetAsync(prefix + "3", "def", expiry: TimeSpan.FromSeconds(4), when: When.NotExists);
        var x3 = db.StringSetAndGetAsync(prefix + "4", "def", expiry: TimeSpan.FromMilliseconds(4001), when: When.NotExists);

        var s0 = db.StringGetAsync(prefix + "1");

        //Act
        var s1 = db.StringGetAsync(prefix + "2");

        //Assert
        (await x0).Should().Be("abc");
        (await x1).Should().Be(RedisValue.Null);
        (await x2).Should().Be(RedisValue.Null);
        (await x3).Should().Be(RedisValue.Null);

        (await s0).Should().Be("abc");
        (await s1).Should().Be("def");
    }

    [Fact]
    public async Task ranges()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v2_1_8);

        var db = conn.GetDatabase();
        var key = Me();

        db.KeyDelete(key, CommandFlags.FireAndForget);

        db.StringSet(key, "abcdefghi", flags: CommandFlags.FireAndForget);
        db.StringSetRange(key, 2, "xy", CommandFlags.FireAndForget);
        db.StringSetRange(key, 4, Encode("z"), CommandFlags.FireAndForget);

        //Act
        var val = db.StringGetAsync(key);

        //Assert
        (await val).Should().Be("abxyzfghi");
    }

    [Fact]
    public async Task incr_decr()
    {
        //Arrange
        await using var conn = Create();

        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        db.StringSet(key, "2", flags: CommandFlags.FireAndForget);
        var v1 = db.StringIncrementAsync(key);
        var v2 = db.StringIncrementAsync(key, 5);
        var v3 = db.StringIncrementAsync(key, -2);
        var v4 = db.StringDecrementAsync(key);
        var v5 = db.StringDecrementAsync(key, 5);
        var v6 = db.StringDecrementAsync(key, -2);

        //Act
        var s = db.StringGetAsync(key);

        //Assert
        (await v1).Should().Be(3);
        (await v2).Should().Be(8);
        (await v3).Should().Be(6);
        (await v4).Should().Be(5);
        (await v5).Should().Be(0);
        (await v6).Should().Be(2);
        (await s).Should().Be("2");
    }

    [Fact]
    public async Task incr_decr_float()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v2_6_0);

        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        db.StringSet(key, "2", flags: CommandFlags.FireAndForget);
        var v1 = db.StringIncrementAsync(key, 1.1);
        var v2 = db.StringIncrementAsync(key, 5.0);
        var v3 = db.StringIncrementAsync(key, -2.0);
        var v4 = db.StringIncrementAsync(key, -1.0);
        var v5 = db.StringIncrementAsync(key, -5.0);
        var v6 = db.StringIncrementAsync(key, 2.0);

        //Act
        var s = db.StringGetAsync(key);

        //Assert
        (await v1).Should().BeApproximately(3.1, 1e-5);
        (await v2).Should().BeApproximately(8.1, 1e-5);
        (await v3).Should().BeApproximately(6.1, 1e-5);
        (await v4).Should().BeApproximately(5.1, 1e-5);
        (await v5).Should().BeApproximately(0.1, 1e-5);
        (await v6).Should().BeApproximately(2.1, 1e-5);
        ((double)await s).Should().BeApproximately(2.1, 1e-5);
    }

    [Fact]
    public async Task get_range()
    {
        //Arrange
        await using var conn = Create();

        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        db.StringSet(key, "abcdefghi", flags: CommandFlags.FireAndForget);
        var s = db.StringGetRangeAsync(key, 2, 4);

        //Act
        var b = db.StringGetRangeAsync(key, 2, 4);

        //Assert
        (await s).Should().Be("cde");
        Decode(await b).Should().Be("cde");
    }

    [Fact]
    public async Task bit_count()
    {
        await using var conn = Create(require: RedisFeatures.v2_6_0);

        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key, flags: CommandFlags.FireAndForget);
        db.StringSet(key, "foobar", flags: CommandFlags.FireAndForget);

        var r1 = db.StringBitCount(key);
        var r2 = db.StringBitCount(key, 0, 0);
        var r3 = db.StringBitCount(key, 1, 1);

        r1.Should().Be(26);
        r2.Should().Be(4);
        r3.Should().Be(6);

        // Async
        r1 = await db.StringBitCountAsync(key);
        r2 = await db.StringBitCountAsync(key, 0, 0);
        r3 = await db.StringBitCountAsync(key, 1, 1);

        r1.Should().Be(26);
        r2.Should().Be(4);
        r3.Should().Be(6);
    }

    [Fact]
    public async Task bit_count_with_bit_unit()
    {
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key, flags: CommandFlags.FireAndForget);
        db.StringSet(key, "foobar", flags: CommandFlags.FireAndForget);

        var r1 = db.StringBitCount(key, 1, 1); // Using default byte
        var r2 = db.StringBitCount(key, 1, 1, StringIndexType.Bit);

        r1.Should().Be(6);
        r2.Should().Be(1);

        // Async
        r1 = await db.StringBitCountAsync(key, 1, 1); // Using default byte
        r2 = await db.StringBitCountAsync(key, 1, 1, StringIndexType.Bit);

        r1.Should().Be(6);
        r2.Should().Be(1);
    }

    [Fact]
    public async Task bit_op()
    {
        await using var conn = Create(require: RedisFeatures.v2_6_0);

        var db = conn.GetDatabase();
        var prefix = Me();
        var key1 = prefix + "1";
        var key2 = prefix + "2";
        var key3 = prefix + "3";
        db.StringSet(key1, new byte[] { 3 }, flags: CommandFlags.FireAndForget);
        db.StringSet(key2, new byte[] { 6 }, flags: CommandFlags.FireAndForget);
        db.StringSet(key3, new byte[] { 12 }, flags: CommandFlags.FireAndForget);

        var len_and = db.StringBitOperationAsync(Bitwise.And, "and", [key1, key2, key3]);
        var len_or = db.StringBitOperationAsync(Bitwise.Or, "or", [key1, key2, key3]);
        var len_xor = db.StringBitOperationAsync(Bitwise.Xor, "xor", [key1, key2, key3]);
        var len_not = db.StringBitOperationAsync(Bitwise.Not, "not", key1);

        (await len_and).Should().Be(1);
        (await len_or).Should().Be(1);
        (await len_xor).Should().Be(1);
        (await len_not).Should().Be(1);

        var r_and = ((byte[]?)(await db.StringGetAsync("and").ForAwait()))?.Single();
        var r_or = ((byte[]?)(await db.StringGetAsync("or").ForAwait()))?.Single();
        var r_xor = ((byte[]?)(await db.StringGetAsync("xor").ForAwait()))?.Single();
        var r_not = ((byte[]?)(await db.StringGetAsync("not").ForAwait()))?.Single();

        r_and.Should().Be((byte)(3 & 6 & 12));
        r_or.Should().Be((byte)(3 | 6 | 12));
        r_xor.Should().Be((byte)(3 ^ 6 ^ 12));
        r_not.Should().Be(unchecked((byte)(~3)));
    }

    [Fact]
    public async Task bit_op_extended()
    {
        await using var conn = Create(require: RedisFeatures.v8_2_0_rc1);
        var db = conn.GetDatabase();
        var prefix = Me();
        var keyX = prefix + "X";
        var keyY1 = prefix + "Y1";
        var keyY2 = prefix + "Y2";
        var keyY3 = prefix + "Y3";

        // Clean up keys
        db.KeyDelete([keyX, keyY1, keyY2, keyY3], CommandFlags.FireAndForget);

        // Set up test data with more complex patterns
        // X = 11110000 (240)
        // Y1 = 10101010 (170)
        // Y2 = 01010101 (85)
        // Y3 = 11001100 (204)
        db.StringSet(keyX, new byte[] { 240 }, flags: CommandFlags.FireAndForget);
        db.StringSet(keyY1, new byte[] { 170 }, flags: CommandFlags.FireAndForget);
        db.StringSet(keyY2, new byte[] { 85 }, flags: CommandFlags.FireAndForget);
        db.StringSet(keyY3, new byte[] { 204 }, flags: CommandFlags.FireAndForget);

        // Test DIFF: X ∧ ¬(Y1 ∨ Y2 ∨ Y3)
        // Y1 ∨ Y2 ∨ Y3 = 170 | 85 | 204 = 255
        // X ∧ ¬(Y1 ∨ Y2 ∨ Y3) = 240 & ~255 = 240 & 0 = 0
        var len_diff = await db.StringBitOperationAsync(Bitwise.Diff, "diff", [keyX, keyY1, keyY2, keyY3]);
        len_diff.Should().Be(1);
        var r_diff = ((byte[]?)(await db.StringGetAsync("diff")))?.Single();
        r_diff.Should().Be((byte)0);

        // Test DIFF1: ¬X ∧ (Y1 ∨ Y2 ∨ Y3)
        // ¬X = ~240 = 15
        // Y1 ∨ Y2 ∨ Y3 = 255
        // ¬X ∧ (Y1 ∨ Y2 ∨ Y3) = 15 & 255 = 15
        var len_diff1 = await db.StringBitOperationAsync(Bitwise.Diff1, "diff1", [keyX, keyY1, keyY2, keyY3]);
        len_diff1.Should().Be(1);
        var r_diff1 = ((byte[]?)(await db.StringGetAsync("diff1")))?.Single();
        r_diff1.Should().Be((byte)15);

        // Test ANDOR: X ∧ (Y1 ∨ Y2 ∨ Y3)
        // Y1 ∨ Y2 ∨ Y3 = 255
        // X ∧ (Y1 ∨ Y2 ∨ Y3) = 240 & 255 = 240
        var len_andor = await db.StringBitOperationAsync(Bitwise.AndOr, "andor", [keyX, keyY1, keyY2, keyY3]);
        len_andor.Should().Be(1);
        var r_andor = ((byte[]?)(await db.StringGetAsync("andor")))?.Single();
        r_andor.Should().Be((byte)240);

        // Test ONE: bits set in exactly one bitmap
        // For X=240, Y1=170, Y2=85, Y3=204
        // We need to count bits that appear in exactly one of these values
        var len_one = await db.StringBitOperationAsync(Bitwise.One, "one", [keyX, keyY1, keyY2, keyY3]);
        len_one.Should().Be(1);
        var r_one = ((byte[]?)(await db.StringGetAsync("one")))?.Single();

        // Calculate expected ONE result manually
        // Bit 7: X=1, Y1=1, Y2=0, Y3=1 -> count=3, not exactly 1
        // Bit 6: X=1, Y1=0, Y2=1, Y3=1 -> count=3, not exactly 1
        // Bit 5: X=1, Y1=1, Y2=0, Y3=0 -> count=2, not exactly 1
        // Bit 4: X=1, Y1=0, Y2=1, Y3=0 -> count=2, not exactly 1
        // Bit 3: X=0, Y1=1, Y2=0, Y3=1 -> count=2, not exactly 1
        // Bit 2: X=0, Y1=0, Y2=1, Y3=1 -> count=2, not exactly 1
        // Bit 1: X=0, Y1=1, Y2=0, Y3=0 -> count=1, exactly 1! -> bit should be set
        // Bit 0: X=0, Y1=0, Y2=1, Y3=0 -> count=1, exactly 1! -> bit should be set
        // Expected result: 00000011 = 3
        r_one.Should().Be((byte)3);
    }

    [Fact]
    public async Task bit_op_two_operands()
    {
        await using var conn = Create(require: RedisFeatures.v8_2_0_rc1);
        var db = conn.GetDatabase();
        var prefix = Me();
        var key1 = prefix + "1";
        var key2 = prefix + "2";

        // Clean up keys
        db.KeyDelete([key1, key2], CommandFlags.FireAndForget);

        // Test with two operands: key1=10101010 (170), key2=11001100 (204)
        db.StringSet(key1, new byte[] { 170 }, flags: CommandFlags.FireAndForget);
        db.StringSet(key2, new byte[] { 204 }, flags: CommandFlags.FireAndForget);

        // Test DIFF: key1 ∧ ¬key2 = 170 & ~204 = 170 & 51 = 34
        var len_diff = await db.StringBitOperationAsync(Bitwise.Diff, "diff2", [key1, key2]);
        len_diff.Should().Be(1);
        var r_diff = ((byte[]?)(await db.StringGetAsync("diff2")))?.Single();
        r_diff.Should().Be((byte)(170 & ~204));

        // Test ONE with two operands (should be equivalent to XOR)
        var len_one = await db.StringBitOperationAsync(Bitwise.One, "one2", [key1, key2]);
        len_one.Should().Be(1);
        var r_one = ((byte[]?)(await db.StringGetAsync("one2")))?.Single();
        r_one.Should().Be((byte)(170 ^ 204));

        // Verify ONE equals XOR for two operands
        var len_xor = await db.StringBitOperationAsync(Bitwise.Xor, "xor2", [key1, key2]);
        len_xor.Should().Be(1);
        var r_xor = ((byte[]?)(await db.StringGetAsync("xor2")))?.Single();
        r_xor.Should().Be(r_one);
    }

    [Fact]
    public async Task bit_op_diff()
    {
        await using var conn = Create(require: RedisFeatures.v8_2_0_rc1);
        var db = conn.GetDatabase();
        var prefix = Me();
        var keyX = prefix + "X";
        var keyY1 = prefix + "Y1";
        var keyY2 = prefix + "Y2";
        var keyResult = prefix + "result";

        // Clean up keys
        db.KeyDelete([keyX, keyY1, keyY2, keyResult], CommandFlags.FireAndForget);

        // Set up test data: X=11110000, Y1=10100000, Y2=01010000
        // Expected DIFF result: X ∧ ¬(Y1 ∨ Y2) = 11110000 ∧ ¬(11110000) = 00000000
        db.StringSet(keyX, new byte[] { 0b11110000 }, flags: CommandFlags.FireAndForget);
        db.StringSet(keyY1, new byte[] { 0b10100000 }, flags: CommandFlags.FireAndForget);
        db.StringSet(keyY2, new byte[] { 0b01010000 }, flags: CommandFlags.FireAndForget);

        var length = db.StringBitOperation(Bitwise.Diff, keyResult, [keyX, keyY1, keyY2]);
        length.Should().Be(1);

        var result = ((byte[]?)db.StringGet(keyResult))?.Single();
        // X ∧ ¬(Y1 ∨ Y2) = 11110000 ∧ ¬(11110000) = 11110000 ∧ 00001111 = 00000000
        result.Should().Be((byte)0b00000000);
    }

    [Fact]
    public async Task bit_op_diff1()
    {
        await using var conn = Create(require: RedisFeatures.v8_2_0_rc1);
        var db = conn.GetDatabase();
        var prefix = Me();
        var keyX = prefix + "X";
        var keyY1 = prefix + "Y1";
        var keyY2 = prefix + "Y2";
        var keyResult = prefix + "result";

        // Clean up keys
        db.KeyDelete([keyX, keyY1, keyY2, keyResult], CommandFlags.FireAndForget);

        // Set up test data: X=11000000, Y1=10100000, Y2=01010000
        // Expected DIFF1 result: ¬X ∧ (Y1 ∨ Y2) = ¬11000000 ∧ (10100000 ∨ 01010000) = 00111111 ∧ 11110000 = 00110000
        db.StringSet(keyX, new byte[] { 0b11000000 }, flags: CommandFlags.FireAndForget);
        db.StringSet(keyY1, new byte[] { 0b10100000 }, flags: CommandFlags.FireAndForget);
        db.StringSet(keyY2, new byte[] { 0b01010000 }, flags: CommandFlags.FireAndForget);

        var length = db.StringBitOperation(Bitwise.Diff1, keyResult, [keyX, keyY1, keyY2]);
        length.Should().Be(1);

        var result = ((byte[]?)db.StringGet(keyResult))?.Single();
        // ¬X ∧ (Y1 ∨ Y2) = 00111111 ∧ 11110000 = 00110000
        result.Should().Be((byte)0b00110000);
    }

    [Fact]
    public async Task bit_op_and_or()
    {
        await using var conn = Create(require: RedisFeatures.v8_2_0_rc1);
        var db = conn.GetDatabase();
        var prefix = Me();
        var keyX = prefix + "X";
        var keyY1 = prefix + "Y1";
        var keyY2 = prefix + "Y2";
        var keyResult = prefix + "result";

        // Clean up keys
        db.KeyDelete([keyX, keyY1, keyY2, keyResult], CommandFlags.FireAndForget);

        // Set up test data: X=11110000, Y1=10100000, Y2=01010000
        // Expected ANDOR result: X ∧ (Y1 ∨ Y2) = 11110000 ∧ (10100000 ∨ 01010000) = 11110000 ∧ 11110000 = 11110000
        db.StringSet(keyX, new byte[] { 0b11110000 }, flags: CommandFlags.FireAndForget);
        db.StringSet(keyY1, new byte[] { 0b10100000 }, flags: CommandFlags.FireAndForget);
        db.StringSet(keyY2, new byte[] { 0b01010000 }, flags: CommandFlags.FireAndForget);

        var length = db.StringBitOperation(Bitwise.AndOr, keyResult, [keyX, keyY1, keyY2]);
        length.Should().Be(1);

        var result = ((byte[]?)db.StringGet(keyResult))?.Single();
        // X ∧ (Y1 ∨ Y2) = 11110000 ∧ 11110000 = 11110000
        result.Should().Be((byte)0b11110000);
    }

    [Fact]
    public async Task bit_op_one()
    {
        await using var conn = Create(require: RedisFeatures.v8_2_0_rc1);
        var db = conn.GetDatabase();
        var prefix = Me();
        var key1 = prefix + "1";
        var key2 = prefix + "2";
        var key3 = prefix + "3";
        var keyResult = prefix + "result";

        // Clean up keys
        db.KeyDelete([key1, key2, key3, keyResult], CommandFlags.FireAndForget);

        // Set up test data: key1=10100000, key2=01010000, key3=00110000
        // Expected ONE result: bits set in exactly one bitmap = 11000000
        db.StringSet(key1, new byte[] { 0b10100000 }, flags: CommandFlags.FireAndForget);
        db.StringSet(key2, new byte[] { 0b01010000 }, flags: CommandFlags.FireAndForget);
        db.StringSet(key3, new byte[] { 0b00110000 }, flags: CommandFlags.FireAndForget);

        var length = db.StringBitOperation(Bitwise.One, keyResult, [key1, key2, key3]);
        length.Should().Be(1);

        var result = ((byte[]?)db.StringGet(keyResult))?.Single();
        // Bits set in exactly one: position 7 (key1 only), position 6 (key2 only) = 11000000
        result.Should().Be((byte)0b11000000);
    }

    [Fact]
    public async Task bit_op_diff_async()
    {
        await using var conn = Create(require: RedisFeatures.v8_2_0_rc1);
        var db = conn.GetDatabase();
        var prefix = Me();
        var keyX = prefix + "X";
        var keyY1 = prefix + "Y1";
        var keyResult = prefix + "result";

        // Clean up keys
        db.KeyDelete([keyX, keyY1, keyResult], CommandFlags.FireAndForget);

        // Set up test data: X=11110000, Y1=10100000
        // Expected DIFF result: X ∧ ¬Y1 = 11110000 ∧ 01011111 = 01010000
        db.StringSet(keyX, new byte[] { 0b11110000 }, flags: CommandFlags.FireAndForget);
        db.StringSet(keyY1, new byte[] { 0b10100000 }, flags: CommandFlags.FireAndForget);

        var length = await db.StringBitOperationAsync(Bitwise.Diff, keyResult, [keyX, keyY1]);
        length.Should().Be(1);

        var result = ((byte[]?)await db.StringGetAsync(keyResult))?.Single();
        // X ∧ ¬Y1 = 11110000 ∧ 01011111 = 01010000
        result.Should().Be((byte)0b01010000);
    }

    [Fact]
    public async Task bit_op_edge_cases()
    {
        await using var conn = Create(require: RedisFeatures.v8_2_0_rc1);
        var db = conn.GetDatabase();
        var prefix = Me();
        var keyEmpty = prefix + "empty";
        var keyNonEmpty = prefix + "nonempty";
        var keyResult = prefix + "result";

        // Clean up keys
        db.KeyDelete([keyEmpty, keyNonEmpty, keyResult], CommandFlags.FireAndForget);

        // Test with empty bitmap
        db.StringSet(keyNonEmpty, new byte[] { 0b11110000 }, flags: CommandFlags.FireAndForget);

        // DIFF with empty key should return the first key
        var length = db.StringBitOperation(Bitwise.Diff, keyResult, [keyNonEmpty, keyEmpty]);
        length.Should().Be(1);

        var result = ((byte[]?)db.StringGet(keyResult))?.Single();
        result.Should().Be((byte)0b11110000);

        // ONE with single key should return that key
        length = db.StringBitOperation(Bitwise.One, keyResult, [keyNonEmpty]);
        length.Should().Be(1);

        result = ((byte[]?)db.StringGet(keyResult))?.Single();
        result.Should().Be((byte)0b11110000);
    }

    [Fact]
    public async Task bit_position()
    {
        await using var conn = Create(require: RedisFeatures.v2_6_0);

        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key, flags: CommandFlags.FireAndForget);
        db.StringSet(key, "foo", flags: CommandFlags.FireAndForget);

        var r1 = db.StringBitPosition(key, true);
        var r2 = db.StringBitPosition(key, true, 10, 10);
        var r3 = db.StringBitPosition(key, true, 1, 3);

        r1.Should().Be(1);
        r2.Should().Be(-1);
        r3.Should().Be(9);

        // Async
        r1 = await db.StringBitPositionAsync(key, true);
        r2 = await db.StringBitPositionAsync(key, true, 10, 10);
        r3 = await db.StringBitPositionAsync(key, true, 1, 3);

        r1.Should().Be(1);
        r2.Should().Be(-1);
        r3.Should().Be(9);
    }

    [Fact]
    public async Task bit_position_with_bit_unit()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key, flags: CommandFlags.FireAndForget);
        db.StringSet(key, "foo", flags: CommandFlags.FireAndForget);

        var r1 = db.StringBitPositionAsync(key, true, 1, 3); // Using default byte

        //Act
        var r2 = db.StringBitPositionAsync(key, true, 1, 3, StringIndexType.Bit);

        //Assert
        (await r1).Should().Be(9);
        (await r2).Should().Be(1);
    }

    [Fact]
    public async Task range_string()
    {
        //Arrange
        await using var conn = Create();

        var db = conn.GetDatabase();
        var key = Me();
        db.StringSet(key, "hello world", flags: CommandFlags.FireAndForget);

        //Act
        var result = db.StringGetRangeAsync(key, 2, 6);

        //Assert
        (await result).Should().Be("llo w");
    }

    [Fact]
    public async Task hash_string_length_async()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v3_2_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string value = "hello world";
        db.HashSet(key, "field", value);
        var resAsync = db.HashStringLengthAsync(key, "field");

        //Act
        var resNonExistingAsync = db.HashStringLengthAsync(key, "non-existing-field");

        //Assert
        (await resAsync).Should().Be(value.Length);
        (await resNonExistingAsync).Should().Be(0);
    }

    [Fact]
    public async Task hash_string_length()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v3_2_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string value = "hello world";

        //Act
        db.HashSet(key, "field", value);

        //Assert
        db.HashStringLength(key, "field").Should().Be(value.Length);
        db.HashStringLength(key, "non-existing-field").Should().Be(0);
    }

    [Fact]
    public async Task longest_common_subsequence()
    {
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var db = conn.GetDatabase();
        var key1 = Me() + "1";
        var key2 = Me() + "2";
        db.KeyDelete(key1);
        db.KeyDelete(key2);
        db.StringSet(key1, "ohmytext");
        db.StringSet(key2, "mynewtext");

        db.StringLongestCommonSubsequence(key1, key2).Should().Be("mytext");
        db.StringLongestCommonSubsequenceLength(key1, key2).Should().Be(6);

        var stringMatchResult = db.StringLongestCommonSubsequenceWithMatches(key1, key2);
        stringMatchResult.Matches.Length.Should().Be(2); // "my" and "text" are the two matches of the result
        stringMatchResult.Matches[0].Should().BeEquivalentTo(new LCSMatchResult.LCSMatch(new(4, 7), new(5, 8), length: 4)); // the string "text" starts at index 4 in the first string and at index 5 in the second string
        stringMatchResult.Matches[1].Should().BeEquivalentTo(new LCSMatchResult.LCSMatch(new(2, 3), new(0, 1), length: 2)); // the string "my" starts at index 2 in the first string and at index 0 in the second string

        stringMatchResult = db.StringLongestCommonSubsequenceWithMatches(key1, key2, 5);
        stringMatchResult.Matches.Should().BeEmpty(); // no matches longer than 5 characters
        stringMatchResult.LongestMatchLength.Should().Be(6);

        // Missing keys
        db.KeyDelete(key1);
        db.StringLongestCommonSubsequence(key1, key2).Should().Be(string.Empty);
        db.KeyDelete(key2);
        db.StringLongestCommonSubsequence(key1, key2).Should().Be(string.Empty);
        stringMatchResult = db.StringLongestCommonSubsequenceWithMatches(key1, key2);
        Assert.NotNull(stringMatchResult.Matches);
        stringMatchResult.Matches.Should().BeEmpty();
        stringMatchResult.LongestMatchLength.Should().Be(0);

        // Default value
        stringMatchResult = db.StringLongestCommonSubsequenceWithMatches(key1, key2, flags: CommandFlags.FireAndForget);
        stringMatchResult.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task longest_common_subsequence_async()
    {
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var db = conn.GetDatabase();
        var key1 = Me() + "1";
        var key2 = Me() + "2";
        db.KeyDelete(key1);
        db.KeyDelete(key2);
        db.StringSet(key1, "ohmytext");
        db.StringSet(key2, "mynewtext");

        (await db.StringLongestCommonSubsequenceAsync(key1, key2)).Should().Be("mytext");
        (await db.StringLongestCommonSubsequenceLengthAsync(key1, key2)).Should().Be(6);

        var stringMatchResult = await db.StringLongestCommonSubsequenceWithMatchesAsync(key1, key2);
        stringMatchResult.Matches.Length.Should().Be(2); // "my" and "text" are the two matches of the result
        stringMatchResult.Matches[0].Should().BeEquivalentTo(new LCSMatchResult.LCSMatch(new(4, 7), new(5, 8), length: 4)); // the string "text" starts at index 4 in the first string and at index 5 in the second string
        stringMatchResult.Matches[1].Should().BeEquivalentTo(new LCSMatchResult.LCSMatch(new(2, 3), new(0, 1), length: 2)); // the string "my" starts at index 2 in the first string and at index 0 in the second string

        stringMatchResult = await db.StringLongestCommonSubsequenceWithMatchesAsync(key1, key2, 5);
        stringMatchResult.Matches.Should().BeEmpty(); // no matches longer than 5 characters
        stringMatchResult.LongestMatchLength.Should().Be(6);

        // Missing keys
        db.KeyDelete(key1);
        (await db.StringLongestCommonSubsequenceAsync(key1, key2)).Should().Be(string.Empty);
        db.KeyDelete(key2);
        (await db.StringLongestCommonSubsequenceAsync(key1, key2)).Should().Be(string.Empty);
        stringMatchResult = await db.StringLongestCommonSubsequenceWithMatchesAsync(key1, key2);
        Assert.NotNull(stringMatchResult.Matches);
        stringMatchResult.Matches.Should().BeEmpty();
        stringMatchResult.LongestMatchLength.Should().Be(0);

        // Default value
        stringMatchResult = await db.StringLongestCommonSubsequenceWithMatchesAsync(key1, key2, flags: CommandFlags.FireAndForget);
        stringMatchResult.IsEmpty.Should().BeTrue();
    }

    private static byte[] Encode(string value) => Encoding.UTF8.GetBytes(value);
    private static string? Decode(byte[]? value) => value is null ? null : Encoding.UTF8.GetString(value);
}
