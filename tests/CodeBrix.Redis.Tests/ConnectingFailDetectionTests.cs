using System;
using System.Threading;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class ConnectingFailDetectionTests(ITestOutputHelper output) : TestBase(output)
{
    protected override string GetConfiguration() => TestConfig.Current.PrimaryServerAndPort + "," + TestConfig.Current.ReplicaServerAndPort;

    [Fact]
    [Trait(TestCategories.Category, TestCategories.SimulatedConnectionFailure)]
    public async Task fast_notices_fail_on_connecting_sync_completion()
    {
        try
        {
            await using var conn = Create(keepAlive: 1, connectTimeout: 10000, allowAdmin: true, allowSimulateConnectionFailure: true);
            conn.RawConfig.ReconnectRetryPolicy = new LinearRetry(200);

            var db = conn.GetDatabase();
            await db.PingAsync();

            var server = conn.GetServer(conn.GetEndPoints()[0]);
            Assert.SkipUnless(server.CanSimulateConnectionFailure(), "Skipping because server cannot simulate connection failure");
            var server2 = conn.GetServer(conn.GetEndPoints()[1]);

            conn.AllowConnect = false;

            // muxer.IsConnected is true of *any* are connected, simulate failure for all cases.
            server.SimulateConnectionFailure(SimulatedFailureType.All);
            server.IsConnected.Should().BeFalse();
            server2.IsConnected.Should().BeTrue();
            conn.IsConnected.Should().BeTrue();

            server2.SimulateConnectionFailure(SimulatedFailureType.All);
            server.IsConnected.Should().BeFalse();
            server2.IsConnected.Should().BeFalse();
            conn.IsConnected.Should().BeFalse();

            // should reconnect within 1 keepalive interval
            conn.AllowConnect = true;
            Log("Waiting for reconnect");
            await UntilConditionAsync(TimeSpan.FromSeconds(2), () => conn.IsConnected).ForAwait();

            conn.IsConnected.Should().BeTrue();
        }
        finally
        {
            ClearAmbientFailures();
        }
    }

    [Fact]
    [Trait(TestCategories.Category, TestCategories.SimulatedConnectionFailure)]
    public async Task fast_notices_fail_on_connecting_async_completion()
    {
        try
        {
            await using var conn = Create(keepAlive: 1, connectTimeout: 10000, allowAdmin: true, allowSimulateConnectionFailure: true);
            conn.RawConfig.ReconnectRetryPolicy = new LinearRetry(200);

            var db = conn.GetDatabase();
            await db.PingAsync();

            var server = conn.GetServer(conn.GetEndPoints()[0]);
            Assert.SkipUnless(server.CanSimulateConnectionFailure(), "Skipping because server cannot simulate connection failure");
            var server2 = conn.GetServer(conn.GetEndPoints()[1]);

            conn.AllowConnect = false;

            // muxer.IsConnected is true of *any* are connected, simulate failure for all cases.
            server.SimulateConnectionFailure(SimulatedFailureType.All);
            server.IsConnected.Should().BeFalse();
            server2.IsConnected.Should().BeTrue();
            conn.IsConnected.Should().BeTrue();

            server2.SimulateConnectionFailure(SimulatedFailureType.All);
            server.IsConnected.Should().BeFalse();
            server2.IsConnected.Should().BeFalse();
            conn.IsConnected.Should().BeFalse();

            // should reconnect within 1 keepalive interval
            conn.AllowConnect = true;
            Log("Waiting for reconnect");
            await UntilConditionAsync(TimeSpan.FromSeconds(2), () => conn.IsConnected).ForAwait();

            conn.IsConnected.Should().BeTrue();
        }
        finally
        {
            ClearAmbientFailures();
        }
    }

    [Fact]
    [Trait(TestCategories.Category, TestCategories.SimulatedConnectionFailure)]
    public async Task issue922_reconnect_raised()
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        var config = ConfigurationOptions.Parse(TestConfig.Current.PrimaryServerAndPort);
        config.AbortOnConnectFail = true;
        config.KeepAlive = 1;
        config.SyncTimeout = 1000;
        config.AsyncTimeout = 1000;
        config.ReconnectRetryPolicy = new ExponentialRetry(5000);
        config.AllowAdmin = true;
        config.AllowSimulateConnectionFailure = true;
        config.BacklogPolicy = BacklogPolicy.FailFast;

        int failCount = 0, restoreCount = 0;

        await using var conn = await ConnectionMultiplexer.ConnectAsync(config);

        conn.ConnectionFailed += (s, e) =>
        {
            Interlocked.Increment(ref failCount);
            Log($"Connection Failed ({e.ConnectionType}, {e.FailureType}): {e.Exception}");
        };
        conn.ConnectionRestored += (s, e) =>
        {
            Interlocked.Increment(ref restoreCount);
            Log($"Connection Restored ({e.ConnectionType}, {e.FailureType})");
        };

        conn.GetDatabase();
        Volatile.Read(ref failCount).Should().Be(0);
        Volatile.Read(ref restoreCount).Should().Be(0);

        var server = conn.GetServer(TestConfig.Current.PrimaryServerAndPort);
        var protocol = server.Protocol;
        // RESP2 has interactive+subscriber connections; RESP3 uses one connection for both.
        var expectedCount = protocol is RedisProtocol.Resp3 ? 1 : 2;
        Log($"Using {protocol.GetString()}; expecting {expectedCount} reconnect event(s)");

        Assert.SkipUnless(server.CanSimulateConnectionFailure(), "Skipping because server cannot simulate connection failure");
        server.SimulateConnectionFailure(SimulatedFailureType.All);

        await UntilConditionAsync(TimeSpan.FromSeconds(10), () => Volatile.Read(ref failCount) >= expectedCount && Volatile.Read(ref restoreCount) >= expectedCount);

        var failCountSnapshot = Volatile.Read(ref failCount);
        (failCountSnapshot >= expectedCount).Should().BeTrue($"failCount {failCountSnapshot} >= {expectedCount} ({protocol.GetString()})");

        var restoreCountSnapshot = Volatile.Read(ref restoreCount);
        (restoreCountSnapshot >= expectedCount).Should().BeTrue($"restoreCount ({restoreCountSnapshot}) >= {expectedCount} ({protocol.GetString()})");
    }

    [Fact]
    public async Task connects_when_begin_connect_completes_synchronously()
    {
        try
        {
            await using var conn = Create(keepAlive: 1, connectTimeout: 3000);

            var db = conn.GetDatabase();
            await db.PingAsync();

            conn.IsConnected.Should().BeTrue();
        }
        finally
        {
            ClearAmbientFailures();
        }
    }

    [Fact]
    public async Task connect_includes_subscriber()
    {
        await using var conn = Create(keepAlive: 1, connectTimeout: 3000, shared: false);

        var db = conn.GetDatabase();
        await db.PingAsync();
        conn.IsConnected.Should().BeTrue();

        foreach (var server in conn.GetServerSnapshot())
        {
            server.InteractiveConnectionState.Should().Be(PhysicalBridge.State.ConnectedEstablished);
            server.SubscriptionConnectionState.Should().Be(PhysicalBridge.State.ConnectedEstablished);
        }
    }
}
