using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[Collection(NonParallelCollection.Name)]
public class LockingTests(ITestOutputHelper output) : TestBase(output)
{
    public enum TestMode
    {
        MultiExec,
        NoMultiExec,
        Twemproxy,
    }

    public static IEnumerable<TheoryDataRow<TestMode>> TestModes()
    {
        yield return new(TestMode.MultiExec);
        yield return new(TestMode.NoMultiExec);
        yield return new(TestMode.Twemproxy);
    }

    [Theory, MemberData(nameof(TestModes))]
    public void aggressive_parallel(TestMode testMode)
    {
        int count = 2;
        int errorCount = 0;
        int bgErrorCount = 0;
        var evt = new ManualResetEvent(false);
        var key = Me() + testMode;
        using (var conn1 = Create(testMode))
        using (var conn2 = Create(testMode))
        {
            void Inner(object? obj)
            {
                try
                {
                    var conn = (IDatabase?)obj!;
                    conn.Multiplexer.ErrorMessage += (sender, e) => Interlocked.Increment(ref errorCount);

                    for (int i = 0; i < 1000; i++)
                    {
                        conn.LockTakeAsync(key, "def", TimeSpan.FromSeconds(5));
                    }
                    conn.Ping();
                    if (Interlocked.Decrement(ref count) == 0) evt.Set();
                }
                catch
                {
                    Interlocked.Increment(ref bgErrorCount);
                }
            }
            int db = testMode == TestMode.Twemproxy ? 0 : 2;
            ThreadPool.QueueUserWorkItem(Inner, conn1.GetDatabase(db));
            ThreadPool.QueueUserWorkItem(Inner, conn2.GetDatabase(db));
            evt.WaitOne(8000);
        }
        Volatile.Read(ref errorCount).Should().Be(0);
        bgErrorCount.Should().Be(0);
    }

    [Fact]
    public async Task test_op_count_by_version_local_up_level()
    {
        await using var conn = Create(shared: false);

        TestLockOpCountByVersion(conn, 1, false);
        TestLockOpCountByVersion(conn, 1, true);
    }

    private void TestLockOpCountByVersion(IConnectionMultiplexer conn, int expectedOps, bool existFirst)
    {
        const int LockDuration = 30;
        RedisKey key = Me();

        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        RedisValue newVal = "us:" + Guid.NewGuid().ToString();
        RedisValue expectedVal = newVal;
        if (existFirst)
        {
            expectedVal = "other:" + Guid.NewGuid().ToString();
            db.StringSet(key, expectedVal, TimeSpan.FromSeconds(LockDuration), flags: CommandFlags.FireAndForget);
        }
        long countBefore = GetServer(conn).GetCounters().Interactive.OperationCount;

        var taken = db.LockTake(key, newVal, TimeSpan.FromSeconds(LockDuration));

        long countAfter = GetServer(conn).GetCounters().Interactive.OperationCount;
        var valAfter = db.StringGet(key);

        taken.Should().Be(!existFirst);
        valAfter.Should().Be(expectedVal);
        // note we get a ping from GetCounters
        (countAfter - countBefore >= expectedOps).Should().BeTrue($"({countAfter} - {countBefore}) >= {expectedOps}");
    }

    private IConnectionMultiplexer Create(TestMode mode) => mode switch
    {
        TestMode.MultiExec => Create(),
        TestMode.NoMultiExec => Create(disabledCommands: ["multi", "exec"]),
        TestMode.Twemproxy => Create(proxy: Proxy.Twemproxy),
        _ => throw new NotSupportedException(mode.ToString()),
    };

    [Theory, MemberData(nameof(TestModes))]
    public async Task take_lock_and_extend(TestMode testMode)
    {
        await using var conn = Create(testMode);

        RedisValue right = Guid.NewGuid().ToString(),
            wrong = Guid.NewGuid().ToString();

        int dbId = testMode == TestMode.Twemproxy ? 0 : 7;
        RedisKey key = Me() + testMode;

        var db = conn.GetDatabase(dbId);

        db.KeyDelete(key, CommandFlags.FireAndForget);

        bool withTran = testMode == TestMode.MultiExec;
        var t1 = db.LockTakeAsync(key, right, TimeSpan.FromSeconds(20));
        var t1b = db.LockTakeAsync(key, wrong, TimeSpan.FromSeconds(10));
        var t2 = db.LockQueryAsync(key);
        var t3 = withTran ? db.LockReleaseAsync(key, wrong) : null;
        var t4 = db.LockQueryAsync(key);
        var t5 = withTran ? db.LockExtendAsync(key, wrong, TimeSpan.FromSeconds(60)) : null;
        var t6 = db.LockQueryAsync(key);
        var t7 = db.KeyTimeToLiveAsync(key);
        var t8 = db.LockExtendAsync(key, right, TimeSpan.FromSeconds(60));
        var t9 = db.LockQueryAsync(key);
        var t10 = db.KeyTimeToLiveAsync(key);
        var t11 = db.LockReleaseAsync(key, right);
        var t12 = db.LockQueryAsync(key);
        var t13 = db.LockTakeAsync(key, wrong, TimeSpan.FromSeconds(10));

        right.Should().NotBe(default(RedisValue));
        wrong.Should().NotBe(default(RedisValue));
        wrong.Should().NotBe(right);
        (await t1).Should().BeTrue("1");
        (await t1b).Should().BeFalse("1b");
        (await t2).Should().Be(right);
        if (withTran) (await t3!).Should().BeFalse("3");
        (await t4).Should().Be(right);
        if (withTran) (await t5!).Should().BeFalse("5");
        (await t6).Should().Be(right);
        var ttl = (await t7)!.Value.TotalSeconds;
        (ttl > 0 && ttl <= 20).Should().BeTrue("7");
        (await t8).Should().BeTrue("8");
        (await t9).Should().Be(right);
        ttl = (await t10)!.Value.TotalSeconds;
        (ttl > 50 && ttl <= 60).Should().BeTrue("10");
        (await t11).Should().BeTrue("11");
        ((string?)await t12).Should().BeNull();
        (await t13).Should().BeTrue("13");
    }

    [Theory, MemberData(nameof(TestModes))]
    public async Task test_basic_lock_not_taken(TestMode testMode)
    {
        await using var conn = Create(testMode);

        int errorCount = 0;
        conn.ErrorMessage += (sender, e) => Interlocked.Increment(ref errorCount);
        Task<bool>? taken = null;
        Task<RedisValue>? newValue = null;
        Task<TimeSpan?>? ttl = null;

        const int LOOP = 50;
        var db = conn.GetDatabase();
        var key = Me() + testMode;
        for (int i = 0; i < LOOP; i++)
        {
            _ = db.KeyDeleteAsync(key);
            taken = db.LockTakeAsync(key, "new-value", TimeSpan.FromSeconds(10));
            newValue = db.StringGetAsync(key);
            ttl = db.KeyTimeToLiveAsync(key);
        }
        (await taken!).Should().BeTrue("taken");
        (await newValue!).Should().Be("new-value");
        var ttlValue = (await ttl!)!.Value.TotalSeconds;
        (ttlValue >= 8 && ttlValue <= 10).Should().BeTrue("ttl");

        errorCount.Should().Be(0);
    }

    [Theory, MemberData(nameof(TestModes))]
    public async Task test_basic_lock_taken(TestMode testMode)
    {
        await using var conn = Create(testMode);

        var db = conn.GetDatabase();
        var key = Me() + testMode;
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.StringSet(key, "old-value", TimeSpan.FromSeconds(20), flags: CommandFlags.FireAndForget);
        var taken = db.LockTakeAsync(key, "new-value", TimeSpan.FromSeconds(10));
        var newValue = db.StringGetAsync(key);
        var ttl = db.KeyTimeToLiveAsync(key);

        (await taken).Should().BeFalse("taken");
        (await newValue).Should().Be("old-value");
        var ttlValue = (await ttl)!.Value.TotalSeconds;
        (ttlValue >= 18 && ttlValue <= 20).Should().BeTrue("ttl");
    }
}
