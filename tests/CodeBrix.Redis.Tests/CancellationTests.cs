using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[Collection(NonParallelCollection.Name)]
public class CancellationTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    [Fact]
    public async Task with_cancellation_cancelled_token_throws_operation_canceled_exception()
    {

        await using var conn = Create();
        var db = conn.GetDatabase();

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await db.StringSetAsync(Me(), "value").WaitAsync(cts.Token));
    }

    private IInternalConnectionMultiplexer Create() => Create(syncTimeout: 10_000);

    [Fact]
    public async Task with_cancellation_valid_token_operation_succeeds()
    {
        await using var conn = Create();
        var db = conn.GetDatabase();

        using var cts = new CancellationTokenSource();

        RedisKey key = Me();
        // This should succeed
        await db.StringSetAsync(key, "value");
        var result = await db.StringGetAsync(key).WaitAsync(cts.Token);
        result.Should().Be("value");
    }

    private static void Pause(IDatabase db) => db.Execute("client", ["pause", ConnectionPauseMilliseconds], CommandFlags.FireAndForget);

    private void Pause(IServer server)
    {
        server.Execute("client", new object[] { "pause", ConnectionPauseMilliseconds }, CommandFlags.FireAndForget);
    }

    [Fact]
    public async Task with_timeout_short_timeout_async_throws_operation_canceled_exception()
    {
        Skip.UnlessLongRunning(); // because of CLIENT PAUSE impact to unrelated tests

        await using var conn = Create();
        var db = conn.GetDatabase();

        var watch = Stopwatch.StartNew();
        Pause(db);

        var timeout = TimeSpan.FromMilliseconds(ShortDelayMilliseconds);
        // This might throw due to timeout, but let's test the mechanism
        var pending = db.StringSetAsync(Me(), "value").WaitAsync(timeout, TestContext.Current.CancellationToken); // check we get past this
        try
        {
            await pending;
            // If it succeeds, that's fine too - Redis is fast
            Assert.Fail(ExpectedCancel + ": " + watch.ElapsedMilliseconds + "ms");
        }
        catch (TimeoutException)
        {
            // Expected for very short timeouts
            Log($"Timeout after {watch.ElapsedMilliseconds}ms");
        }
    }

    private const string ExpectedCancel = "This operation should have been cancelled";

    [Fact]
    public async Task without_cancellation_operations_work_normally()
    {
        await using var conn = Create();
        var db = conn.GetDatabase();

        // No cancellation - should work normally
        RedisKey key = Me();
        await db.StringSetAsync(key, "value");
        var result = await db.StringGetAsync(key);
        result.Should().Be("value");
    }

    public enum CancelStrategy
    {
        Constructor,
        Method,
        Manual,
    }

    private const int ConnectionPauseMilliseconds = 50, ShortDelayMilliseconds = 5;

    private static CancellationTokenSource CreateCts(CancelStrategy strategy)
    {
        switch (strategy)
        {
            case CancelStrategy.Constructor:
                return new CancellationTokenSource(TimeSpan.FromMilliseconds(ShortDelayMilliseconds));
            case CancelStrategy.Method:
                var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromMilliseconds(ShortDelayMilliseconds));
                return cts;
            case CancelStrategy.Manual:
                cts = new();
                _ = Task.Run(async () =>
                {
                    await Task.Delay(ShortDelayMilliseconds, TestContext.Current.CancellationToken);
                    // ReSharper disable once MethodHasAsyncOverload - TFM-dependent
                    cts.Cancel();
                });
                return cts;
            default:
                throw new ArgumentOutOfRangeException(nameof(strategy));
        }
    }

    [Theory]
    [InlineData(CancelStrategy.Constructor)]
    [InlineData(CancelStrategy.Method)]
    [InlineData(CancelStrategy.Manual)]
    public async Task cancellation_during_operation_async_cancels_gracefully(CancelStrategy strategy)
    {
        Skip.UnlessLongRunning(); // because of CLIENT PAUSE impact to unrelated tests

        await using var conn = Create();
        var db = conn.GetDatabase();

        var watch = Stopwatch.StartNew();
        Pause(db);

        // Cancel after a short delay
        using var cts = CreateCts(strategy);

        // Start an operation and cancel it mid-flight
        var pending = db.StringSetAsync($"{Me()}:{strategy}", "value").WaitAsync(cts.Token);

        try
        {
            await pending;
            Assert.Fail(ExpectedCancel + ": " + watch.ElapsedMilliseconds + "ms");
        }
        catch (OperationCanceledException oce)
        {
            // Expected if cancellation happens during operation
            Log($"Cancelled after {watch.ElapsedMilliseconds}ms");
            oce.CancellationToken.Should().Be(cts.Token);
        }
    }

    [Fact]
    public async Task scan_cancellable()
    {
        Skip.UnlessLongRunning(); // because of CLIENT PAUSE impact to unrelated tests

        using var conn = Create();
        var db = conn.GetDatabase();
        var server = conn.GetServer(conn.GetEndPoints()[0]);

        using var cts = new CancellationTokenSource();

        var watch = Stopwatch.StartNew();
        Pause(server);
        try
        {
            db.StringSet(Me(), "value", TimeSpan.FromMinutes(5), flags: CommandFlags.FireAndForget);
            await using var iter = server.KeysAsync(pageSize: 1000).WithCancellation(cts.Token).GetAsyncEnumerator();
            var pending = iter.MoveNextAsync();
            cts.Token.IsCancellationRequested.Should().BeFalse();
            cts.CancelAfter(ShortDelayMilliseconds); // start this *after* we've got past the initial check
            while (await pending)
            {
                pending = iter.MoveNextAsync();
            }
            Assert.Fail($"{ExpectedCancel}: {watch.ElapsedMilliseconds}ms");
        }
        catch (OperationCanceledException oce)
        {
            var taken = watch.ElapsedMilliseconds;
            // Expected if cancellation happens during operation
            Log($"Cancelled after {taken}ms");
            (taken < (ConnectionPauseMilliseconds * 3) / 4).Should().BeTrue($"Should have cancelled sooner; took {taken}ms");
            oce.CancellationToken.Should().Be(cts.Token);
        }
    }
}
