using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[Collection(NonParallelCollection.Name)]
public class AsyncTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    [Trait(TestCategories.Category, TestCategories.SimulatedConnectionFailure)]
    public async Task async_tasks_report_failure_if_server_unavailable()
    {
        SetExpectedAmbientFailureCount(-1); // this will get messy

        await using var conn = Create(allowAdmin: true, backlogPolicy: BacklogPolicy.FailFast, allowSimulateConnectionFailure: true);
        var server = conn.GetServer(TestConfig.Current.PrimaryServer, TestConfig.Current.PrimaryPort);
        Assert.SkipUnless(server.CanSimulateConnectionFailure(), "Skipping because server cannot simulate connection failure");

        RedisKey key = Me();
        var db = conn.GetDatabase();
        db.KeyDelete(key);
        var a = db.SetAddAsync(key, "a");
        var b = db.SetAddAsync(key, "b");

        conn.Wait(a).Should().BeTrue();
        conn.Wait(b).Should().BeTrue();

        conn.AllowConnect = false;

        server.SimulateConnectionFailure(SimulatedFailureType.All);
        var c = db.SetAddAsync(key, "c");

        c.IsFaulted.Should().BeTrue("faulted");
        Assert.NotNull(c.Exception);
        var ex = c.Exception.InnerExceptions.Single();
        ex.Should().BeOfType<RedisConnectionException>();
        ex.Message.Should().StartWith("No connection is active/available to service this operation: SADD " + key.ToString());
    }

    [Fact]
    public async Task async_timeout_is_noticed()
    {
        await using var conn = Create(syncTimeout: 1000, asyncTimeout: 1000, allowAdmin: true);
        await using var pauseConn = Create(allowAdmin: true);
        var opt = ConfigurationOptions.Parse(conn.Configuration);
        if (!Debugger.IsAttached)
        { // we max the timeouts if a debugger is detected
            opt.AsyncTimeout.Should().Be(1000);
        }

        RedisKey key = Me();
        var val = Guid.NewGuid().ToString();
        var db = conn.GetDatabase();
        db.StringSet(key, val);

        conn.GetStatus().Should().Contain("; async timeouts: 0;");

        // This is done on another connection, because it queues a SELECT due to being an unknown command that will not timeout
        // at the head of the queue
        await pauseConn.GetDatabase().ExecuteAsync("client", "pause", 4000).ForAwait(); // client pause returns immediately

        var ms = Stopwatch.StartNew();
        var ex = await Assert.ThrowsAsync<RedisTimeoutException>(async () =>
        {
            Log("Issuing StringGetAsync");
            await db.StringGetAsync(key).ForAwait(); // but *subsequent* operations are paused
            ms.Stop();
            Log($"Unexpectedly succeeded after {ms.ElapsedMilliseconds}ms");
        }).ForAwait();
        ms.Stop();
        Log($"Timed out after {ms.ElapsedMilliseconds}ms");

        Log("Exception message: " + ex.Message);
        ex.Message.Should().Contain("Timeout awaiting response");
        // Ensure we are including the last payload size
        ex.Message.Should().Contain("last-in:");
        ex.Message.Should().NotContain("last-in: 0");
        Assert.NotNull(ex.Data["Redis-Last-Result-Bytes"]);
        ex.Message.Should().Contain("cur-in:");

        string status = conn.GetStatus();
        Log(status);
        status.Should().Contain("; async timeouts: 1;");
    }
}
