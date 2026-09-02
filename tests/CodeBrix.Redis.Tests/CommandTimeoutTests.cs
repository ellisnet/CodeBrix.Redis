using System;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[Collection(NonParallelCollection.Name)]
public class CommandTimeoutTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task default_heartbeat_timeout()
    {
        Skip.UnlessLongRunning();
        var options = ConfigurationOptions.Parse(TestConfig.Current.PrimaryServerAndPort);
        options.AllowAdmin = true;
        options.AsyncTimeout = 1000;

        await using var pauseConn = ConnectionMultiplexer.Connect(options);
        await using var conn = ConnectionMultiplexer.Connect(options);

        var pauseServer = GetServer(pauseConn);
        var pauseTask = pauseServer.ExecuteAsync("CLIENT", "PAUSE", 5000);

        var key = Me();
        var db = conn.GetDatabase();
        var sw = ValueStopwatch.StartNew();
        var ex = await Assert.ThrowsAsync<RedisTimeoutException>(async () => await db.StringGetAsync(key));
        Log(ex.Message);
        var duration = sw.GetElapsedTime();
        (duration < TimeSpan.FromSeconds(4000)).Should().BeTrue($"Duration ({duration.Milliseconds} ms) should be less than 4000ms");

        // Await as to not bias the next test
        await pauseTask;
    }

#if DEBUG
    [Fact]
    public async Task default_heartbeat_low_timeout()
    {
        //gated: this test connects with ConnectionMultiplexer.ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        //(It is a DEBUG-only test, so it is invisible to a Release sweep.)
        Skip.IfNoContainers();

        var options = ConfigurationOptions.Parse(TestConfig.Current.PrimaryServerAndPort);
        options.AllowAdmin = true;
        options.AsyncTimeout = 50;
        options.HeartbeatInterval = TimeSpan.FromMilliseconds(100);

        await using var pauseConn = await ConnectionMultiplexer.ConnectAsync(options);
        await using var conn = await ConnectionMultiplexer.ConnectAsync(options);

        var pauseServer = GetServer(pauseConn);
        var pauseTask = pauseServer.ExecuteAsync("CLIENT", "PAUSE", 2000);

        var key = Me();
        var db = conn.GetDatabase();
        var sw = ValueStopwatch.StartNew();
        var ex = await Assert.ThrowsAsync<RedisTimeoutException>(async () => await db.StringGetAsync(key));
        Log(ex.Message);
        var duration = sw.GetElapsedTime();
        (duration < TimeSpan.FromSeconds(250)).Should().BeTrue($"Duration ({duration.Milliseconds} ms) should be less than 250ms");

        // Await as to not bias the next test
        await pauseTask;
    }
#endif
}
