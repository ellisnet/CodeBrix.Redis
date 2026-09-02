using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CodeBrix.Redis.KeyspaceIsolation;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[RunPerProtocol]
public class BasicOpsTests(ITestOutputHelper output, SharedConnectionFixture fixture)
    : BasicOpsTestsBase(output, fixture, null)
{
}

/*
[RunPerProtocol]
public class InProcBasicOpsTests(ITestOutputHelper output, InProcServerFixture fixture)
    : BasicOpsTestsBase(output, null, fixture)
{
    protected override bool UseDedicatedInProcessServer => true;
}
*/

[RunPerProtocol]
public abstract class BasicOpsTestsBase(ITestOutputHelper output, SharedConnectionFixture? connection, InProcServerFixture? server)
    : TestBase(output, connection, server)
{
    [Fact]
    public async Task ping_once()
    {
        //Arrange
        await using var conn = ConnectFactory();
        var db = conn.GetDatabase();
        var duration = await db.PingAsync().ForAwait();

        //Act
        Log("Ping took: " + duration);

        //Assert
        (duration.TotalMilliseconds > 0).Should().BeTrue();
    }

    [Fact]
    public async Task rapid_dispose()
    {
        SkipIfWouldUseRealServer("This needs some CI love, it's not a scenario we care about too much but noisy atm.");
        await using var primary = ConnectFactory();
        var db = primary.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        for (int i = 0; i < 10; i++)
        {
            await using var secondary = primary.CreateClient();
            secondary.GetDatabase().StringIncrement(key, flags: CommandFlags.FireAndForget);
        }
        // Give it a moment to get through the pipe...they were fire and forget
        await UntilConditionAsync(TimeSpan.FromSeconds(5), () => 10 == (int)db.StringGet(key));
        ((int)db.StringGet(key)).Should().Be(10);
    }

    [Fact]
    public async Task ping_many()
    {
        await using var conn = ConnectFactory();
        var db = conn.GetDatabase();
        var tasks = new Task<TimeSpan>[100];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = db.PingAsync();
        }
        await Task.WhenAll(tasks).ForAwait();
        (tasks[0].Result.TotalMilliseconds > 0).Should().BeTrue();
        (tasks[tasks.Length - 1].Result.TotalMilliseconds > 0).Should().BeTrue();
    }

    [Fact]
    public async Task get_with_null_key()
    {
        //Arrange
        await using var conn = ConnectFactory();
        var db = conn.GetDatabase();
        const string? key = null;

        //Act
        var ex = Assert.Throws<ArgumentException>(() => db.StringGet(key));

        //Assert
        ex.Message.Should().Be("A null key is not valid in this context");
    }

    [Fact]
    public async Task set_with_null_key()
    {
        //Arrange
        await using var conn = ConnectFactory();
        var db = conn.GetDatabase();
        const string? key = null, value = "abc";

        //Act
        var ex = Assert.Throws<ArgumentException>(() => db.StringSet(key!, value));

        //Assert
        ex.Message.Should().Be("A null key is not valid in this context");
    }

    [Fact]
    public async Task set_with_null_value()
    {
        await using var conn = ConnectFactory();
        var db = conn.GetDatabase();
        string key = Me();
        const string? value = null;
        db.KeyDelete(key, CommandFlags.FireAndForget);

        db.StringSet(key, "abc", flags: CommandFlags.FireAndForget);
        db.KeyExists(key).Should().BeTrue();
        db.StringSet(key, value, flags: CommandFlags.FireAndForget);

        var actual = (string?)db.StringGet(key);
        actual.Should().BeNull();
        db.KeyExists(key).Should().BeFalse();
    }

    [Fact]
    public async Task set_with_default_value()
    {
        await using var conn = ConnectFactory();
        var db = conn.GetDatabase();
        string key = Me();
        var value = default(RedisValue); // this is kinda 0... ish
        db.KeyDelete(key, CommandFlags.FireAndForget);

        db.StringSet(key, "abc", flags: CommandFlags.FireAndForget);
        db.KeyExists(key).Should().BeTrue();
        db.StringSet(key, value, flags: CommandFlags.FireAndForget);

        var actual = (string?)db.StringGet(key);
        actual.Should().BeNull();
        db.KeyExists(key).Should().BeFalse();
    }

    [Fact]
    public async Task set_with_zero_value()
    {
        await using var conn = ConnectFactory();
        var db = conn.GetDatabase();
        string key = Me();
        const long value = 0;
        db.KeyDelete(key, CommandFlags.FireAndForget);

        db.StringSet(key, "abc", flags: CommandFlags.FireAndForget);
        db.KeyExists(key).Should().BeTrue();
        db.StringSet(key, value, flags: CommandFlags.FireAndForget);

        var actual = (string?)db.StringGet(key);
        actual.Should().Be("0");
        db.KeyExists(key).Should().BeTrue();
    }

    [Fact]
    public async Task get_set_async()
    {
        await using var conn = ConnectFactory();
        var db = conn.GetDatabase();

        RedisKey key = Me();
        var d0 = db.KeyDeleteAsync(key);
        var d1 = db.KeyDeleteAsync(key);
        var g1 = db.StringGetAsync(key);
        var s1 = db.StringSetAsync(key, "123");
        var g2 = db.StringGetAsync(key);
        var d2 = db.KeyDeleteAsync(key);

        await d0;
        (await d1).Should().BeFalse();
        ((string?)(await g1)).Should().BeNull();
        ((await g1).IsNull).Should().BeTrue();
        await s1;
        (await g2).Should().Be("123");
        ((int)(await g2)).Should().Be(123);
        ((await g2).IsNull).Should().BeFalse();
        (await d2).Should().BeTrue();
    }

    [Fact]
    public async Task get_set_sync()
    {
        //Arrange
        await using var conn = ConnectFactory();
        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        var d1 = db.KeyDelete(key);
        var g1 = db.StringGet(key);
        db.StringSet(key, "123", flags: CommandFlags.FireAndForget);
        var g2 = db.StringGet(key);

        //Act
        var d2 = db.KeyDelete(key);

        //Assert
        d1.Should().BeFalse();
        ((string?)g1).Should().BeNull();
        g1.IsNull.Should().BeTrue();
        g2.Should().Be("123");
        ((int)g2).Should().Be(123);
        g2.IsNull.Should().BeFalse();
        d2.Should().BeTrue();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task get_with_expiry(bool exists, bool hasExpiry)
    {
        await using var conn = ConnectFactory();
        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        if (exists)
        {
            if (hasExpiry)
                db.StringSet(key, "val", TimeSpan.FromMinutes(5), flags: CommandFlags.FireAndForget);
            else
                db.StringSet(key, "val", flags: CommandFlags.FireAndForget);
        }
        var async = db.StringGetWithExpiryAsync(key);
        var syncResult = db.StringGetWithExpiry(key);
        var asyncResult = await async;

        if (exists)
        {
            asyncResult.Value.Should().Be("val");
            asyncResult.Expiry.HasValue.Should().Be(hasExpiry);
            if (hasExpiry) (asyncResult.Expiry!.Value.TotalMinutes >= 4.9 && asyncResult.Expiry.Value.TotalMinutes <= 5).Should().BeTrue();
            syncResult.Value.Should().Be("val");
            syncResult.Expiry.HasValue.Should().Be(hasExpiry);
            if (hasExpiry) (syncResult.Expiry!.Value.TotalMinutes >= 4.9 && syncResult.Expiry.Value.TotalMinutes <= 5).Should().BeTrue();
        }
        else
        {
            asyncResult.Value.IsNull.Should().BeTrue();
            asyncResult.Expiry.HasValue.Should().BeFalse();
            syncResult.Value.IsNull.Should().BeTrue();
            syncResult.Expiry.HasValue.Should().BeFalse();
        }
    }

    [Fact]
    public async Task get_with_expiry_wrong_type_async()
    {
        await using var conn = ConnectFactory();
        var db = conn.GetDatabase();
        RedisKey key = Me();
        _ = db.KeyDeleteAsync(key);
        _ = db.SetAddAsync(key, "abc");
        var ex = await Assert.ThrowsAsync<RedisServerException>(async () =>
        {
            try
            {
                Log("Key: " + (string?)key);
                await db.StringGetWithExpiryAsync(key).ForAwait();
            }
            catch (AggregateException e)
            {
                throw e.InnerExceptions[0];
            }
        }).ForAwait();
        ex.Message.Should().Be("WRONGTYPE Operation against a key holding the wrong kind of value");
    }

    [Fact]
    public async Task get_with_expiry_wrong_type_sync()
    {
        await using var conn = ConnectFactory();
        var db = conn.GetDatabase();
        RedisKey key = Me();
        var ex = await Assert.ThrowsAsync<RedisServerException>(async () =>
        {
            db.KeyDelete(key, CommandFlags.FireAndForget);
            db.SetAdd(key, "abc", CommandFlags.FireAndForget);
            db.StringGetWithExpiry(key);
        });
        ex.Message.Should().Be("WRONGTYPE Operation against a key holding the wrong kind of value");
    }

#if DEBUG
    [Fact]
    [Trait(TestCategories.Category, TestCategories.SimulatedConnectionFailure)]
    public async Task test_severed()
    {
        await using var conn = Create(allowAdmin: true, allowSimulateConnectionFailure: true);
        var db = conn.GetDatabase();
        string key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.StringSet(key, key);
        var server = GetServer(conn);
        Assert.SkipUnless(server.CanSimulateConnectionFailure(), "Skipping because server cannot simulate connection failure");

        SetExpectedAmbientFailureCount(2);
        server.SimulateConnectionFailure(SimulatedFailureType.All);
        var watch = Stopwatch.StartNew();
        await UntilConditionAsync(TimeSpan.FromSeconds(10), () => server.IsConnected);
        watch.Stop();
        Log("Time to re-establish: {0}ms (any order)", watch.ElapsedMilliseconds);
        await UntilConditionAsync(TimeSpan.FromSeconds(10), () => key == db.StringGet(key));
        Debug.WriteLine("Pinging...");
        db.StringGet(key).Should().Be(key);
    }
#endif

    [Fact]
    public async Task incr_async()
    {
        //Arrange
        await using var conn = ConnectFactory();
        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        var nix = db.KeyExistsAsync(key).ForAwait();
        var a = db.StringGetAsync(key).ForAwait();
        var b = db.StringIncrementAsync(key).ForAwait();
        var c = db.StringGetAsync(key).ForAwait();
        var d = db.StringIncrementAsync(key, 10).ForAwait();
        var e = db.StringGetAsync(key).ForAwait();
        var f = db.StringDecrementAsync(key, 11).ForAwait();
        var g = db.StringGetAsync(key).ForAwait();

        //Act
        var h = db.KeyExistsAsync(key).ForAwait();

        //Assert
        (await nix).Should().BeFalse();
        ((await a).IsNull).Should().BeTrue();
        ((long)(await a)).Should().Be(0);
        (await b).Should().Be(1);
        ((long)(await c)).Should().Be(1);
        (await d).Should().Be(11);
        ((long)(await e)).Should().Be(11);
        (await f).Should().Be(0);
        ((long)(await g)).Should().Be(0);
        (await h).Should().BeTrue();
    }

    [Fact]
    public async Task incr_sync()
    {
        //Arrange
        await using var conn = ConnectFactory();
        var db = conn.GetDatabase();
        RedisKey key = Me();
        Log(key);
        db.KeyDelete(key, CommandFlags.FireAndForget);
        var nix = db.KeyExists(key);
        var a = db.StringGet(key);
        var b = db.StringIncrement(key);
        var c = db.StringGet(key);
        var d = db.StringIncrement(key, 10);
        var e = db.StringGet(key);
        var f = db.StringDecrement(key, 11);
        var g = db.StringGet(key);

        //Act
        var h = db.KeyExists(key);

        //Assert
        nix.Should().BeFalse();
        a.IsNull.Should().BeTrue();
        ((long)a).Should().Be(0);
        b.Should().Be(1);
        ((long)c).Should().Be(1);
        d.Should().Be(11);
        ((long)e).Should().Be(11);
        f.Should().Be(0);
        ((long)g).Should().Be(0);
        h.Should().BeTrue();
    }

    [Fact]
    public async Task incr_different_sizes()
    {
        await using var conn = ConnectFactory();
        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        int expected = 0;
        Incr(db, key, -129019, ref expected);
        Incr(db, key, -10023, ref expected);
        Incr(db, key, -9933, ref expected);
        Incr(db, key, -23, ref expected);
        Incr(db, key, -7, ref expected);
        Incr(db, key, -1, ref expected);
        Incr(db, key, 0, ref expected);
        Incr(db, key, 1, ref expected);
        Incr(db, key, 9, ref expected);
        Incr(db, key, 11, ref expected);
        Incr(db, key, 345, ref expected);
        Incr(db, key, 4982, ref expected);
        Incr(db, key, 13091, ref expected);
        Incr(db, key, 324092, ref expected);
        expected.Should().NotBe(0);
        var sum = (long)db.StringGet(key);
        sum.Should().Be(expected);
    }

    private static void Incr(IDatabase database, RedisKey key, int delta, ref int total)
    {
        database.StringIncrement(key, delta, CommandFlags.FireAndForget);
        total += delta;
    }

    [Fact]
    public async Task delete()
    {
        //Arrange
        await using var conn = ConnectFactory();
        var db = conn.GetDatabase();
        var key = Me();
        _ = db.StringSetAsync(key, "Heyyyyy");
        var ke1 = db.KeyExistsAsync(key).ForAwait();
        var ku1 = db.KeyDelete(key);

        //Act
        var ke2 = db.KeyExistsAsync(key).ForAwait();

        //Assert
        (await ke1).Should().BeTrue();
        ku1.Should().BeTrue();
        (await ke2).Should().BeFalse();
    }

    [Fact]
    public async Task delete_async()
    {
        //Arrange
        await using var conn = ConnectFactory();
        var db = conn.GetDatabase();
        var key = Me();
        _ = db.StringSetAsync(key, "Heyyyyy");
        var ke1 = db.KeyExistsAsync(key).ForAwait();
        var ku1 = db.KeyDeleteAsync(key).ForAwait();

        //Act
        var ke2 = db.KeyExistsAsync(key).ForAwait();

        //Assert
        (await ke1).Should().BeTrue();
        (await ku1).Should().BeTrue();
        (await ke2).Should().BeFalse();
    }

    [Fact]
    public async Task delete_many()
    {
        await using var conn = ConnectFactory();
        var db = conn.GetDatabase();
        var key1 = Me();
        var key2 = Me() + "2";
        var key3 = Me() + "3";
        _ = db.StringSetAsync(key1, "Heyyyyy");
        _ = db.StringSetAsync(key2, "Heyyyyy");
        // key 3 not set
        var ku1 = db.KeyDelete([key1, key2, key3]);
        var ke1 = db.KeyExistsAsync(key1).ForAwait();
        var ke2 = db.KeyExistsAsync(key2).ForAwait();
        ku1.Should().Be(2);
        (await ke1).Should().BeFalse();
        (await ke2).Should().BeFalse();
    }

    [Fact]
    public async Task delete_many_async()
    {
        await using var conn = ConnectFactory();
        var db = conn.GetDatabase();
        var key1 = Me();
        var key2 = Me() + "2";
        var key3 = Me() + "3";
        _ = db.StringSetAsync(key1, "Heyyyyy");
        _ = db.StringSetAsync(key2, "Heyyyyy");
        // key 3 not set
        var ku1 = db.KeyDeleteAsync([key1, key2, key3]).ForAwait();
        var ke1 = db.KeyExistsAsync(key1).ForAwait();
        var ke2 = db.KeyExistsAsync(key2).ForAwait();
        (await ku1).Should().Be(2);
        (await ke1).Should().BeFalse();
        (await ke2).Should().BeFalse();
    }

    [Fact]
    public async Task wrapped_database_prefix_integration()
    {
        //Arrange
        var key = Me();
        await using var conn = ConnectFactory();
        var db = conn.GetDatabase().WithKeyPrefix("abc");
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.StringIncrement(key, flags: CommandFlags.FireAndForget);
        db.StringIncrement(key, flags: CommandFlags.FireAndForget);
        db.StringIncrement(key, flags: CommandFlags.FireAndForget);

        //Act
        int count = (int)conn.GetDatabase().StringGet("abc" + key);

        //Assert
        count.Should().Be(3);
    }

    [Fact]
    public async Task transaction_sync()
    {
        await using var conn = ConnectFactory();
        Assert.SkipUnless(conn.DefaultClient.RawConfig.CommandMap.IsAvailable(RedisCommand.MULTI), "MULTI is not available");
        var db = conn.GetDatabase();

        RedisKey key = Me();

        var tran = db.CreateTransaction();
        _ = db.KeyDeleteAsync(key);
        var x = tran.StringIncrementAsync(Me());
        var y = tran.StringIncrementAsync(Me());
        var z = tran.StringIncrementAsync(Me());
        tran.Execute().Should().BeTrue();
        x.Result.Should().Be(1);
        y.Result.Should().Be(2);
        z.Result.Should().Be(3);
    }

    [Fact]
    public async Task transaction_async()
    {
        await using var conn = ConnectFactory();
        Assert.SkipUnless(conn.DefaultClient.RawConfig.CommandMap.IsAvailable(RedisCommand.MULTI), "MULTI is not available");

        var db = conn.GetDatabase();

        RedisKey key = Me();

        var tran = db.CreateTransaction();
        _ = db.KeyDeleteAsync(key);
        var x = tran.StringIncrementAsync(Me());
        var y = tran.StringIncrementAsync(Me());
        var z = tran.StringIncrementAsync(Me());
        (await tran.ExecuteAsync()).Should().BeTrue();
        (await x).Should().Be(1);
        (await y).Should().Be(2);
        (await z).Should().Be(3);
    }
}
