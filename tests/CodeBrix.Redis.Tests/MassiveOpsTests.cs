using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[Collection(NonParallelCollection.Name)]
public class MassiveOpsTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task long_running()
    {
        Skip.UnlessLongRunning();
        await using var conn = Create();

        var key = Me();
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.StringSet(key, "test value", flags: CommandFlags.FireAndForget);
        for (var i = 0; i < 200; i++)
        {
            var val = await db.StringGetAsync(key).ForAwait();
            val.Should().Be("test value");
            await Task.Delay(50, TestContext.Current.CancellationToken).ForAwait();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task massive_bulk_ops_async(bool withContinuation)
    {
        await using var conn = Create();

        RedisKey key = Me();
        var db = conn.GetDatabase();
        await db.PingAsync().ForAwait();
        static void NonTrivial(Task unused)
        {
            Thread.SpinWait(5);
        }
        var watch = Stopwatch.StartNew();
        for (int i = 0; i <= AsyncOpsQty; i++)
        {
            var t = db.StringSetAsync(key, i);
            if (withContinuation)
            {
                // Intentionally unawaited
                _ = t.ContinueWith(NonTrivial);
            }
        }
        (await db.StringGetAsync(key).ForAwait()).Should().Be(AsyncOpsQty);
        watch.Stop();
        Log($"{Me()}: Time for {AsyncOpsQty} ops: {watch.ElapsedMilliseconds}ms ({(withContinuation ? "with continuation" : "no continuation")}, any order); ops/s: {AsyncOpsQty / watch.Elapsed.TotalSeconds}");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(50)]
    public async Task massive_bulk_ops_sync(int threads)
    {
        Skip.UnlessLongRunning();
        await using var conn = Create(syncTimeout: 30000);

        RedisKey key = Me();
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        int workPerThread = SyncOpsQty / threads;
        var timeTaken = RunConcurrent(
            () =>
            {
                for (int i = 0; i < workPerThread; i++)
                {
                    db.StringIncrement(key, flags: CommandFlags.FireAndForget);
                }
            },
            threads);

        int val = (int)db.StringGet(key);
        val.Should().Be(workPerThread * threads);
        Log($"{Me()}: Time for {threads * workPerThread} ops on {threads} threads: {timeTaken.TotalMilliseconds}ms (any order); ops/s: {(workPerThread * threads) / timeTaken.TotalSeconds}");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public async Task massive_bulk_ops_fire_and_forget(int threads)
    {
        await using var conn = Create(syncTimeout: 30000);

        RedisKey key = Me();
        var db = conn.GetDatabase();
        await db.PingAsync();

        db.KeyDelete(key, CommandFlags.FireAndForget);
        int perThread = AsyncOpsQty / threads;
        var elapsed = RunConcurrent(
            () =>
            {
                for (int i = 0; i < perThread; i++)
                {
                    db.StringIncrement(key, flags: CommandFlags.FireAndForget);
                }
                db.Ping();
            },
            threads);
        var val = (long)db.StringGet(key);
        val.Should().Be(perThread * threads);

        Log($"{Me()}: Time for {val} ops over {threads} threads: {elapsed.TotalMilliseconds:###,###}ms (any order); ops/s: {val / elapsed.TotalSeconds:###,###,##0}");
    }
}
