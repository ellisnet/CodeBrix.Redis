using System.Globalization;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;
using static CodeBrix.Redis.RedisValue;

namespace CodeBrix.Redis.Tests.Issues; //was previously: StackExchange.Redis.Tests.Issues;

public class Issue1103Tests(ITestOutputHelper output) : TestBase(output)
{
    [Theory]
    [InlineData(142205255210238005UL, (int)StorageType.Int64, (int)StorageType.Int64)]
    [InlineData(ulong.MaxValue, (int)StorageType.UInt64, (int)StorageType.UInt64)] // 20-byte canonical uint => UInt64 on read
    [InlineData(ulong.MinValue, (int)StorageType.Int64, (int)StorageType.ShortBlob)]
    [InlineData(0x8000000000000000UL, (int)StorageType.UInt64, (int)StorageType.UInt64)] // long.MaxValue+1: 19-byte canonical uint => UInt64 on read
    [InlineData(0x8000000000000001UL, (int)StorageType.UInt64, (int)StorageType.UInt64)]
    [InlineData(0x7FFFFFFFFFFFFFFFUL, (int)StorageType.Int64, (int)StorageType.Int64)] // long.MaxValue: 19-byte canonical int => Int64 on read
    public async Task large_u_int_64_stored_correctly(ulong value, int storageType, int fromRedisType)
    {
        await using var conn = Create();

        RedisKey key = Me();
        var db = conn.GetDatabase();
        RedisValue typed = value;
        typed.ToString().Should().Be(value.ToString());

        // only need UInt64 for 64-bits
        typed.Type.Should().Be((StorageType)storageType);
        db.StringSet(key, typed);
        var fromRedis = db.StringGet(key);

        Log($"{fromRedis.Type}: {fromRedis}");
        fromRedis.Type.Should().Be((StorageType)fromRedisType);
        ((ulong)fromRedis).Should().Be(value);
        fromRedis.ToString().Should().Be(value.ToString(CultureInfo.InvariantCulture));

        var simplified = fromRedis.Simplify();
        Log($"{simplified.Type}: {simplified}");
        simplified.Type.Should().Be((StorageType)storageType);
        ((ulong)simplified).Should().Be(value);
        fromRedis.ToString().Should().Be(value.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void unusual_redis_value_oddities() // things we found while doing this
    {
        RedisValue x = 0, y = "0";
        y.Should().Be(x);
        x.Should().Be(y);

        y = "-0";
        y.Should().Be(x);
        x.Should().Be(y);

        y = "-"; // this is the oddness; this used to return true
        y.Should().NotBe(x);
        x.Should().NotBe(y);

        y = "+";
        y.Should().NotBe(x);
        x.Should().NotBe(y);

        y = ".";
        y.Should().NotBe(x);
        x.Should().NotBe(y);
    }
}
