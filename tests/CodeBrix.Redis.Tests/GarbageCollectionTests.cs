using System;
using System.Threading;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[Collection(NonParallelCollection.Name)] // because I need to measure some things that could get confused
public class GarbageCollectionTests(ITestOutputHelper helper) : TestBase(helper)
{
    private static void ForceGC()
    {
        for (int i = 0; i < 3; i++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
        }
    }

    [Fact]
    public async Task muxer_is_collected()
    {
#if DEBUG
        Assert.Skip("Only predictable in release builds");
#endif
        // this is more nuanced than it looks; multiple sockets with
        // async callbacks, plus a heartbeat on a timer

        // deliberately not "using" - we *want* to leak this
        var conn = Create();
        await conn.GetDatabase().PingAsync(); // smoke-test

        ForceGC();

// #if DEBUG // this counter only exists in debug
//            int before = ConnectionMultiplexer.CollectedWithoutDispose;
// #endif
        var wr = new WeakReference(conn);
        conn = null;

        for (int i = 0; i < 5 && wr.IsAlive; i++)
        {
            ForceGC();
            await Task.Delay(2000, TestContext.Current.CancellationToken).ForAwait(); // GC is twitchy
            ForceGC();
        }

        // should be collectable
        wr.Target.Should().BeNull();
        // just to ensure we wrote conn, and to suppress a warning
        conn.Should().BeNull();

// #if DEBUG // this counter only exists in debug
//            int after = ConnectionMultiplexer.CollectedWithoutDispose;
//            after.Should().Be(before + 1);
// #endif
    }

    [Fact]
    [Trait(TestCategories.Category, TestCategories.SimulatedConnectionFailure)]
    public async Task unrooted_backlogged_async_task_is_completed_on_timeout()
    {
        Skip.UnlessLongRunning();
        // Run the test on a separate thread without keeping a reference to the task to ensure
        // that there are no references to the variables in test task from the main thread.
        // WithTimeout must not be used within Task.Run because timers are rooted and would keep everything alive.
        var startGC = new TaskCompletionSource<bool>();
        Task? completedTestTask = null;
        _ = Task.Run(async () =>
        {
            await using var conn = await ConnectionMultiplexer.ConnectAsync(
                new ConfigurationOptions()
                {
                    BacklogPolicy = BacklogPolicy.Default,
                    AbortOnConnectFail = false,
                    ConnectTimeout = 50,
                    SyncTimeout = 1000,
                    AllowAdmin = true,
                    AllowSimulateConnectionFailure = true,
                    EndPoints = { GetConfiguration() },
                },
                Writer);
            var db = conn.GetDatabase();

            // Disconnect and don't allow re-connection
            conn.AllowConnect = false;
            var server = conn.GetServerSnapshot()[0];
            server.SimulateConnectionFailure(SimulatedFailureType.All);
            conn.IsConnected.Should().BeFalse();

            var pingTask = Assert.ThrowsAsync<RedisConnectionException>(() => db.PingAsync());
            startGC.SetResult(true);
            await pingTask;
        }).ContinueWith(testTask => Volatile.Write(ref completedTestTask, testTask));

        // Use sync wait and sleep to ensure a more timely GC.
        var timeoutTask = Task.Delay(5000, TestContext.Current.CancellationToken);
        Task.WaitAny(startGC.Task, timeoutTask);
        while (Volatile.Read(ref completedTestTask) == null && !timeoutTask.IsCompleted)
        {
            ForceGC();
            Thread.Sleep(200);
        }

        var testTask = Volatile.Read(ref completedTestTask);
        if (testTask == null) Assert.Fail("Timeout.");

        await testTask;
    }
}
