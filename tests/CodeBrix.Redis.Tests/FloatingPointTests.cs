using System;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class FloatingPointTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    private static bool Within(double x, double y, double delta) => Math.Abs(x - y) <= delta;

    [Fact]
    public async Task incr_decr_floating_point()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        double[] incr =
        [
            12.134,
            -14561.0000002,
            125.3421,
            -2.49892498,
        ],
        decr =
        [
            99.312,
            12,
            -35,
        ];
        double sum = 0;
        foreach (var value in incr)
        {
            db.StringIncrement(key, value, CommandFlags.FireAndForget);
            sum += value;
        }
        foreach (var value in decr)
        {
            db.StringDecrement(key, value, CommandFlags.FireAndForget);
            sum -= value;
        }
        var val = (double)db.StringGet(key);

        Within(sum, val, 0.0001).Should().BeTrue();
    }

    [Fact]
    public async Task incr_decr_floating_point_async()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        double[] incr =
        [
            12.134,
            -14561.0000002,
            125.3421,
            -2.49892498,
        ],
        decr =
        [
            99.312,
            12,
            -35,
        ];
        double sum = 0;
        foreach (var value in incr)
        {
            await db.StringIncrementAsync(key, value).ForAwait();
            sum += value;
        }
        foreach (var value in decr)
        {
            await db.StringDecrementAsync(key, value).ForAwait();
            sum -= value;
        }
        var val = (double)await db.StringGetAsync(key).ForAwait();

        Within(sum, val, 0.0001).Should().BeTrue();
    }

    [Fact]
    public async Task hash_incr_decr_floating_point()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        RedisKey key = Me();
        RedisValue field = "foo";
        db.KeyDelete(key, CommandFlags.FireAndForget);
        double[] incr =
        [
            12.134,
            -14561.0000002,
            125.3421,
            -2.49892498,
        ],
        decr =
        [
            99.312,
            12,
            -35,
        ];
        double sum = 0;
        foreach (var value in incr)
        {
            db.HashIncrement(key, field, value, CommandFlags.FireAndForget);
            sum += value;
        }
        foreach (var value in decr)
        {
            db.HashDecrement(key, field, value, CommandFlags.FireAndForget);
            sum -= value;
        }
        var val = (double)db.HashGet(key, field);

        Within(sum, val, 0.0001).Should().BeTrue($"{sum} not within 0.0001 of {val}");
    }

    [Fact]
    public async Task hash_incr_decr_floating_point_async()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        RedisKey key = Me();
        RedisValue field = "bar";
        db.KeyDelete(key, CommandFlags.FireAndForget);
        double[] incr =
        [
            12.134,
            -14561.0000002,
            125.3421,
            -2.49892498,
        ],
        decr =
        [
            99.312,
            12,
            -35,
        ];
        double sum = 0;
        foreach (var value in incr)
        {
            _ = db.HashIncrementAsync(key, field, value);
            sum += value;
        }
        foreach (var value in decr)
        {
            _ = db.HashDecrementAsync(key, field, value);
            sum -= value;
        }
        var val = (double)await db.HashGetAsync(key, field).ForAwait();

        Within(sum, val, 0.0001).Should().BeTrue();
    }
}
