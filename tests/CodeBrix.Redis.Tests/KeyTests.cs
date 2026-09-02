using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[RunPerProtocol]
public class KeyTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    [Fact]
    public async Task test_scan()
    {
        NoConcurrentRuntime();

        await using var conn = Create(allowAdmin: true);

        var dbId = TestConfig.GetDedicatedDB(conn);
        var db = conn.GetDatabase(dbId);
        var server = GetAnyPrimary(conn);
        var prefix = Me();
        server.FlushDatabase(dbId, flags: CommandFlags.FireAndForget);

        const int Count = 1000;
        for (int i = 0; i < Count; i++)
            db.StringSet(prefix + "x" + i, "y" + i, flags: CommandFlags.FireAndForget);

        var count = server.Keys(dbId, prefix + "*").Count();
        count.Should().Be(Count);
    }

    [Fact]
    public async Task flush_fetch_random_key()
    {
        NoConcurrentRuntime();

        await using var conn = Create(allowAdmin: true);

        var dbId = TestConfig.GetDedicatedDB(conn);
        Skip.IfMissingDatabase(conn, dbId);
        var db = conn.GetDatabase(dbId);
        var prefix = Me();
        conn.GetServer(TestConfig.Current.PrimaryServerAndPort).FlushDatabase(dbId, CommandFlags.FireAndForget);
        string? anyKey = db.KeyRandom();

        anyKey.Should().BeNull();
        db.StringSet(prefix + "abc", "def");
        byte[]? keyBytes = db.KeyRandom();

        Assert.NotNull(keyBytes);
        Encoding.UTF8.GetString(keyBytes).Should().Be(prefix + "abc");
    }

    [Fact]
    public async Task key_type_of_missing_key_is_none() // see #3156
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        db.KeyType(key).Should().Be(RedisType.None);
        (await db.KeyTypeAsync(key)).Should().Be(RedisType.None);

        db.StringSet(key, "abc", flags: CommandFlags.FireAndForget);
        db.KeyType(key).Should().Be(RedisType.String);
        (await db.KeyTypeAsync(key)).Should().Be(RedisType.String);

        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyType(key).Should().Be(RedisType.None);
        (await db.KeyTypeAsync(key)).Should().Be(RedisType.None);
    }

    [Fact]
    public async Task zeros()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.StringSet(key, 123, flags: CommandFlags.FireAndForget);
        int k = (int)db.StringGet(key);
        k.Should().Be(123);

        db.KeyDelete(key, CommandFlags.FireAndForget);
        int i = (int)db.StringGet(key);
        i.Should().Be(0);

        db.StringGet(key).IsNull.Should().BeTrue();
        int? value = (int?)db.StringGet(key);
        value.HasValue.Should().BeFalse();
    }

    [Fact]
    public void prepend_append()
    {
        {
            // simple
            RedisKey key = "world";
            var ret = key.Prepend("hello");
            ret.Should().Be("helloworld");
        }

        {
            RedisKey key1 = "world";
            RedisKey key2 = Encoding.UTF8.GetBytes("hello");
            var key3 = key1.Prepend(key2);
            ReferenceEquals(key1.KeyValue, key3.KeyValue).Should().BeTrue();
            ReferenceEquals(key2.KeyValue, key3.KeyPrefix).Should().BeTrue();
            key3.Should().Be("helloworld");
        }

        {
            RedisKey key = "hello";
            var ret = key.Append("world");
            ret.Should().Be("helloworld");
        }

        {
            RedisKey key1 = Encoding.UTF8.GetBytes("hello");
            RedisKey key2 = "world";
            var key3 = key1.Append(key2);
            ReferenceEquals(key2.KeyValue, key3.KeyValue).Should().BeTrue();
            ReferenceEquals(key1.KeyValue, key3.KeyPrefix).Should().BeTrue();
            key3.Should().Be("helloworld");
        }
    }

    [Fact]
    public async Task exists()
    {
        await using var conn = Create();

        RedisKey key = Me();
        RedisKey key2 = Me() + "2";
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyDelete(key2, CommandFlags.FireAndForget);

        db.KeyExists(key).Should().BeFalse();
        db.KeyExists(key2).Should().BeFalse();
        db.KeyExists([key, key2]).Should().Be(0);

        db.StringSet(key, "new value", flags: CommandFlags.FireAndForget);
        db.KeyExists(key).Should().BeTrue();
        db.KeyExists(key2).Should().BeFalse();
        db.KeyExists([key, key2]).Should().Be(1);

        db.StringSet(key2, "new value", flags: CommandFlags.FireAndForget);
        db.KeyExists(key).Should().BeTrue();
        db.KeyExists(key2).Should().BeTrue();
        db.KeyExists([key, key2]).Should().Be(2);
    }

    [Fact]
    public async Task exists_async()
    {
        await using var conn = Create();

        RedisKey key = Me();
        RedisKey key2 = Me() + "2";
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyDelete(key2, CommandFlags.FireAndForget);
        var a1 = db.KeyExistsAsync(key).ForAwait();
        var a2 = db.KeyExistsAsync(key2).ForAwait();
        var a3 = db.KeyExistsAsync([key, key2]).ForAwait();

        db.StringSet(key, "new value", flags: CommandFlags.FireAndForget);

        var b1 = db.KeyExistsAsync(key).ForAwait();
        var b2 = db.KeyExistsAsync(key2).ForAwait();
        var b3 = db.KeyExistsAsync([key, key2]).ForAwait();

        db.StringSet(key2, "new value", flags: CommandFlags.FireAndForget);

        var c1 = db.KeyExistsAsync(key).ForAwait();
        var c2 = db.KeyExistsAsync(key2).ForAwait();
        var c3 = db.KeyExistsAsync([key, key2]).ForAwait();

        (await a1).Should().BeFalse();
        (await a2).Should().BeFalse();
        (await a3).Should().Be(0);

        (await b1).Should().BeTrue();
        (await b2).Should().BeFalse();
        (await b3).Should().Be(1);

        (await c1).Should().BeTrue();
        (await c2).Should().BeTrue();
        (await c3).Should().Be(2);
    }

    [Fact]
    public async Task key_encoding()
    {
        await using var conn = Create();

        var key = Me();
        var db = conn.GetDatabase();

        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.StringSet(key, "new value", flags: CommandFlags.FireAndForget);

        (db.KeyEncoding(key) is "embstr" or "raw").Should().BeTrue(); // server-version dependent
        (await db.KeyEncodingAsync(key) is "embstr" or "raw").Should().BeTrue();

        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.ListLeftPush(key, "new value", flags: CommandFlags.FireAndForget);

        // Depending on server version, this is going to vary - we're sanity checking here.
        var listTypes = new[] { "ziplist", "quicklist", "listpack" };
        listTypes.Should().Contain(db.KeyEncoding(key));
        listTypes.Should().Contain(await db.KeyEncodingAsync(key));

        var keyNotExists = key + "no-exist";
        db.KeyEncoding(keyNotExists).Should().BeNull();
        (await db.KeyEncodingAsync(keyNotExists)).Should().BeNull();
    }

    [Fact]
    public async Task key_ref_count()
    {
        await using var conn = Create();

        var key = Me();
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.StringSet(key, "new value", flags: CommandFlags.FireAndForget);

        db.KeyRefCount(key).Should().Be(1);
        (await db.KeyRefCountAsync(key)).Should().Be(1);

        var keyNotExists = key + "no-exist";
        db.KeyRefCount(keyNotExists).Should().BeNull();
        (await db.KeyRefCountAsync(keyNotExists)).Should().BeNull();
    }

    [Fact]
    public async Task key_frequency()
    {
        await using var conn = Create(allowAdmin: true, require: RedisFeatures.v4_0_0);

        var key = Me();
        var db = conn.GetDatabase();
        var server = GetServer(conn);

        var serverConfig = server.ConfigGet("maxmemory-policy");
        var maxMemoryPolicy = serverConfig.Length == 1 ? serverConfig[0].Value : "";
        Log($"maxmemory-policy detected as {maxMemoryPolicy}");
        var isLfu = maxMemoryPolicy.Contains("lfu");

        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.StringSet(key, "new value", flags: CommandFlags.FireAndForget);
        db.StringGet(key);

        if (isLfu)
        {
            var count = db.KeyFrequency(key);
            (count > 0).Should().BeTrue();

            count = await db.KeyFrequencyAsync(key);
            (count > 0).Should().BeTrue();

            // Key not exists
            db.KeyDelete(key, CommandFlags.FireAndForget);
            var res = db.KeyFrequency(key);
            res.Should().BeNull();

            res = await db.KeyFrequencyAsync(key);
            res.Should().BeNull();
        }
        else
        {
            var ex = Assert.Throws<RedisServerException>(() => db.KeyFrequency(key));
            ex.Message.Should().Contain("An LFU maxmemory policy is not selected");
            ex = await Assert.ThrowsAsync<RedisServerException>(() => db.KeyFrequencyAsync(key));
            ex.Message.Should().Contain("An LFU maxmemory policy is not selected");
        }
    }

    private static void TestTotalLengthAndCopyTo(in RedisKey key, int expectedLength)
    {
        var length = key.TotalLength();
        length.Should().Be(expectedLength);
        var arr = ArrayPool<byte>.Shared.Rent(length + 20); // deliberately over-sized
        try
        {
            var written = key.CopyTo(arr);
            written.Should().Be(length);

            var viaCast = (byte[]?)key;
            ReadOnlySpan<byte> x = viaCast, y = new ReadOnlySpan<byte>(arr, 0, length);
            x.SequenceEqual(y).Should().BeTrue();
            (key.IsNull == viaCast is null).Should().BeTrue();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(arr);
        }
    }

    [Fact]
    public void null_key_slot()
    {
        //Arrange
        RedisKey key = RedisKey.Null;
        key.TryGetSimpleBuffer(out var buffer).Should().BeTrue();
        buffer.Should().BeEmpty();

        //Act
        TestTotalLengthAndCopyTo(key, 0);

        //Assert
        GetHashSlot(key).Should().Be(-1);
    }

    private static readonly byte[] KeyPrefix = Encoding.UTF8.GetBytes("abcde");

    private static int GetHashSlot(in RedisKey key)
    {
        var strategy = new ServerSelectionStrategy(null!)
        {
            ServerType = ServerType.Cluster,
        };
        return strategy.HashSlot(key);
    }

    [Theory]
    [InlineData(false, null, -1)]
    [InlineData(false, "", 0)]
    [InlineData(false, "f", 3168)]
    [InlineData(false, "abcde", 16097)]
    [InlineData(false, "abcdef", 15101)]
    [InlineData(false, "abcdeffsdkjhsdfgkjh sdkjhsdkjf hsdkjfh skudrfy7 348iu yksef78 dssdhkfh ##$OIU", 5073)]
    [InlineData(false, "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Cras lobortis quam ac molestie ultricies. Duis maximus, nunc a auctor faucibus, risus turpis porttitor nibh, sit amet consequat lacus nibh quis nisi. Aliquam ipsum quam, dapibus ut ex eu, efficitur vestibulum dui. Sed a nibh ut felis congue tempor vel vel lectus. Phasellus a neque placerat, blandit massa sed, imperdiet urna. Praesent scelerisque lorem ipsum, non facilisis libero hendrerit quis. Nullam sit amet malesuada velit, ac lacinia lacus. Donec mollis a massa sed egestas. Suspendisse vitae augue quis erat gravida consectetur. Aenean interdum neque id lacinia eleifend.", 4954)]
    [InlineData(true, null, 16097)]
    [InlineData(true, "", 16097)] // note same as false/abcde
    [InlineData(true, "f", 15101)] // note same as false/abcdef
    [InlineData(true, "abcde", 4089)]
    [InlineData(true, "abcdef", 1167)]
    [InlineData(true, "👻👩‍👩‍👦‍👦", 8494)]
    [InlineData(true, "abcdeffsdkjhsdfgkjh sdkjhsdkjf hsdkjfh skudrfy7 348iu yksef78 dssdhkfh ##$OIU", 10923)]
    [InlineData(true, "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Cras lobortis quam ac molestie ultricies. Duis maximus, nunc a auctor faucibus, risus turpis porttitor nibh, sit amet consequat lacus nibh quis nisi. Aliquam ipsum quam, dapibus ut ex eu, efficitur vestibulum dui. Sed a nibh ut felis congue tempor vel vel lectus. Phasellus a neque placerat, blandit massa sed, imperdiet urna. Praesent scelerisque lorem ipsum, non facilisis libero hendrerit quis. Nullam sit amet malesuada velit, ac lacinia lacus. Donec mollis a massa sed egestas. Suspendisse vitae augue quis erat gravida consectetur. Aenean interdum neque id lacinia eleifend.", 4452)]
    public void test_string_key_slot(bool prefixed, string? s, int slot)
    {
        RedisKey key = prefixed ? new RedisKey(KeyPrefix, s) : s;
        if (s is null && !prefixed)
        {
            key.TryGetSimpleBuffer(out var buffer).Should().BeTrue();
            buffer.Should().BeEmpty();
            TestTotalLengthAndCopyTo(key, 0);
        }
        else
        {
            key.TryGetSimpleBuffer(out var _).Should().BeFalse();
        }
        TestTotalLengthAndCopyTo(key, Encoding.UTF8.GetByteCount(s ?? "") + (prefixed ? KeyPrefix.Length : 0));

        GetHashSlot(key).Should().Be(slot);
    }

    [Theory]
    [InlineData(false, -1, -1)]
    [InlineData(false, 0, 0)]
    [InlineData(false, 1, 10242)]
    [InlineData(false, 6, 10015)]
    [InlineData(false, 47, 849)]
    [InlineData(false, 14123, 2356)]
    [InlineData(true, -1, 16097)]
    [InlineData(true, 0, 16097)]
    [InlineData(true, 1, 7839)]
    [InlineData(true, 6, 6509)]
    [InlineData(true, 47, 2217)]
    [InlineData(true, 14123, 6773)]
    public void test_blob_key_slot(bool prefixed, int count, int slot)
    {
        byte[]? blob = null;
        if (count >= 0)
        {
            blob = new byte[count];
            new Random(count).NextBytes(blob);
            for (int i = 0; i < blob.Length; i++)
            {
                if (blob[i] == (byte)'{') blob[i] = (byte)'!'; // avoid unexpected hash tags
            }
        }
        RedisKey key = prefixed ? new RedisKey(KeyPrefix, blob) : blob;
        if (prefixed)
        {
            key.TryGetSimpleBuffer(out _).Should().BeFalse();
        }
        else
        {
            key.TryGetSimpleBuffer(out var buffer).Should().BeTrue();
            if (blob is null)
            {
                buffer.Should().BeEmpty();
            }
            else
            {
                buffer.Should().BeSameAs(blob);
            }
        }
        TestTotalLengthAndCopyTo(key, (blob?.Length ?? 0) + (prefixed ? KeyPrefix.Length : 0));

        GetHashSlot(key).Should().Be(slot);
    }

    [Theory]
    [MemberData(nameof(KeyEqualityData))]
    public void key_equality(RedisKey x, RedisKey y, bool equal)
    {
        if (equal)
        {
            y.Should().Be(x);
            (x == y).Should().BeTrue();
            (x != y).Should().BeFalse();
            x.Equals(y).Should().BeTrue();
            x.Equals((object)y).Should().BeTrue();
            y.GetHashCode().Should().Be(x.GetHashCode());
        }
        else
        {
            y.Should().NotBe(x);
            (x == y).Should().BeFalse();
            (x != y).Should().BeTrue();
            x.Equals(y).Should().BeFalse();
            x.Equals((object)y).Should().BeFalse();
            // note that this last one is not strictly required, but: we pass, so: yay!
            y.GetHashCode().Should().NotBe(x.GetHashCode());
        }
    }

    public static IEnumerable<TheoryDataRow<RedisKey, RedisKey, bool>> KeyEqualityData()
    {
        RedisKey abcString = "abc", abcBytes = Encoding.UTF8.GetBytes("abc");
        RedisKey abcdefString = "abcdef", abcdefBytes = Encoding.UTF8.GetBytes("abcdef");

        yield return new(RedisKey.Null, abcString, false);
        yield return new(RedisKey.Null, abcBytes, false);
        yield return new(abcString, RedisKey.Null, false);
        yield return new(abcBytes, RedisKey.Null, false);
        yield return new(RedisKey.Null, RedisKey.Null, true);
        yield return new(new RedisKey((string?)null), RedisKey.Null, true);
        yield return new(new RedisKey(null, (byte[]?)null), RedisKey.Null, true);
        yield return new(new RedisKey(""), RedisKey.Null, false);
        yield return new(new RedisKey(null, Array.Empty<byte>()), RedisKey.Null, false);

        yield return new(abcString, abcString, true);
        yield return new(abcBytes, abcBytes, true);
        yield return new(abcString, abcBytes, true);
        yield return new(abcBytes, abcString, true);

        yield return new(abcdefString, abcdefString, true);
        yield return new(abcdefBytes, abcdefBytes, true);
        yield return new(abcdefString, abcdefBytes, true);
        yield return new(abcdefBytes, abcdefString, true);

        yield return new(abcString, abcdefString, false);
        yield return new(abcBytes, abcdefBytes, false);
        yield return new(abcString, abcdefBytes, false);
        yield return new(abcBytes, abcdefString, false);

        yield return new(abcdefString, abcString, false);
        yield return new(abcdefBytes, abcBytes, false);
        yield return new(abcdefString, abcBytes, false);
        yield return new(abcdefBytes, abcString, false);

        var x = abcString.Append("def");
        yield return new(abcdefString, x, true);
        yield return new(abcdefBytes, x, true);
        yield return new(x, abcdefBytes, true);
        yield return new(x, abcdefString, true);
        yield return new(abcString, x, false);
        yield return new(abcString, x, false);
        yield return new(x, abcString, false);
        yield return new(x, abcString, false);
    }
}
