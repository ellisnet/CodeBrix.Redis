using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class MSetTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    [Theory]
    [InlineData(0, When.Always)]
    [InlineData(1, When.Always)]
    [InlineData(2, When.Always)]
    [InlineData(10, When.Always)]
    [InlineData(0, When.NotExists)]
    [InlineData(1, When.NotExists)]
    [InlineData(2, When.NotExists)]
    [InlineData(10, When.NotExists)]
    [InlineData(0, When.NotExists, true)]
    [InlineData(1, When.NotExists, true)]
    [InlineData(2, When.NotExists, true)]
    [InlineData(10, When.NotExists, true)]
    [InlineData(0, When.Exists)]
    [InlineData(1, When.Exists)]
    [InlineData(2, When.Exists)]
    [InlineData(10, When.Exists)]
    [InlineData(0, When.Exists, true)]
    [InlineData(1, When.Exists, true)]
    [InlineData(2, When.Exists, true)]
    [InlineData(10, When.Exists, true)]
    public async Task add_without_expiration(int count, When when, bool precreate = false)
    {
        await using var conn = Create(require: (when == When.Exists && count > 1) ? RedisFeatures.v8_4_0_rc1 : null);
        var pairs = new KeyValuePair<RedisKey, RedisValue>[count];
        var key = Me();
        for (int i = 0; i < count; i++)
        {
            // note the unusual braces; this is to force (on cluster) a hash-slot based on key
            pairs[i] = new KeyValuePair<RedisKey, RedisValue>($"{{{key}}}_{i}", $"value {i}");
        }

        var keys = Array.ConvertAll(pairs, pair => pair.Key);
        var db = conn.GetDatabase();
        // set initial state
        await db.KeyDeleteAsync(keys, flags: CommandFlags.FireAndForget);
        if (precreate)
        {
            foreach (var pair in pairs)
            {
                await db.StringSetAsync(pair.Key, "dummy value", flags: CommandFlags.FireAndForget);
            }
        }

        bool expected = count != 0 & when switch
        {
            When.Always => true,
            When.Exists => precreate,
            When.NotExists => !precreate,
            _ => throw new ArgumentOutOfRangeException(nameof(when)),
        };

        // issue the test command
        var actualPending = db.StringSetAsync(pairs, when);
        var values = await db.StringGetAsync(keys); // pipelined
        var actual = await actualPending;

        // check the state *after* the command
        actual.Should().Be(expected);
        values.Length.Should().Be(count);
        for (int i = 0; i < count; i++)
        {
            if (expected)
            {
                values[i].Should().Be(pairs[i].Value);
            }
            else
            {
                values[i].Should().NotBe(pairs[i].Value);
            }
        }
    }

    [Theory]
    [InlineData(0, When.Always)]
    [InlineData(1, When.Always)]
    [InlineData(2, When.Always)]
    [InlineData(10, When.Always)]
    [InlineData(0, When.NotExists)]
    [InlineData(1, When.NotExists)]
    [InlineData(2, When.NotExists)]
    [InlineData(10, When.NotExists)]
    [InlineData(0, When.NotExists, true)]
    [InlineData(1, When.NotExists, true)]
    [InlineData(2, When.NotExists, true)]
    [InlineData(10, When.NotExists, true)]
    [InlineData(0, When.Exists)]
    [InlineData(1, When.Exists)]
    [InlineData(2, When.Exists)]
    [InlineData(10, When.Exists)]
    [InlineData(0, When.Exists, true)]
    [InlineData(1, When.Exists, true)]
    [InlineData(2, When.Exists, true)]
    [InlineData(10, When.Exists, true)]
    public async Task add_with_relative_expiration(int count, When when, bool precreate = false)
    {
        await using var conn = Create(require: count > 1 ? RedisFeatures.v8_4_0_rc1 : null);
        var pairs = new KeyValuePair<RedisKey, RedisValue>[count];
        var key = Me();
        for (int i = 0; i < count; i++)
        {
            // note the unusual braces; this is to force (on cluster) a hash-slot based on key
            pairs[i] = new KeyValuePair<RedisKey, RedisValue>($"{{{key}}}_{i}", $"value {i}");
        }
        var expiry = TimeSpan.FromMinutes(10);

        var keys = Array.ConvertAll(pairs, pair => pair.Key);
        var db = conn.GetDatabase();
        // set initial state
        await db.KeyDeleteAsync(keys, flags: CommandFlags.FireAndForget);
        if (precreate)
        {
            foreach (var pair in pairs)
            {
                await db.StringSetAsync(pair.Key, "dummy value", flags: CommandFlags.FireAndForget);
            }
        }

        bool expected = count != 0 & when switch
        {
            When.Always => true,
            When.Exists => precreate,
            When.NotExists => !precreate,
            _ => throw new ArgumentOutOfRangeException(nameof(when)),
        };

        // issue the test command
        var actualPending = db.StringSetAsync(pairs, when, expiry);
        Task<TimeSpan?>[] ttls = new Task<TimeSpan?>[count];
        for (int i = 0; i < count; i++)
        {
            ttls[i] = db.KeyTimeToLiveAsync(keys[i]);
        }
        await Task.WhenAll(ttls);
        var values = await db.StringGetAsync(keys); // pipelined
        var actual = await actualPending;

        // check the state *after* the command
        actual.Should().Be(expected);
        values.Length.Should().Be(count);
        for (int i = 0; i < count; i++)
        {
            var ttl = await ttls[i];
            if (expected)
            {
                values[i].Should().Be(pairs[i].Value);
                ttl.Should().NotBeNull();
                (ttl > TimeSpan.Zero && ttl <= expiry).Should().BeTrue();
            }
            else
            {
                values[i].Should().NotBe(pairs[i].Value);
                ttl.Should().BeNull();
            }
        }
    }
}
