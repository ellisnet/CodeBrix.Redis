using System;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class BacklogTests(ITestOutputHelper output) : TestBase(output)
{
    protected override string GetConfiguration() => TestConfig.Current.PrimaryServerAndPort + "," + TestConfig.Current.ReplicaServerAndPort;

    [Fact]
    [Trait(TestCategories.Category, TestCategories.SimulatedConnectionFailure)]
    public async Task fail_fast()
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        void PrintSnapshot(ConnectionMultiplexer muxer)
        {
            Log("Snapshot summary:");
            foreach (var server in muxer.GetServerSnapshot())
            {
                Log($"  {server.EndPoint}: ");
                Log($"     Type: {server.ServerType}");
                Log($"     IsConnected: {server.IsConnected}");
                Log($"      IsConnecting: {server.IsConnecting}");
                Log($"      IsSelectable(allowDisconnected: true): {server.IsSelectable(RedisCommand.PING, true)}");
                Log($"      IsSelectable(allowDisconnected: false): {server.IsSelectable(RedisCommand.PING, false)}");
                Log($"      UnselectableFlags: {server.GetUnselectableFlags()}");
                var bridge = server.GetBridge(RedisCommand.PING, create: false);
                Log($"      GetBridge: {bridge}");
                Log($"        IsConnected: {bridge?.IsConnected}");
                Log($"        ConnectionState: {bridge?.ConnectionState}");
            }
        }

        try
        {
            // Ensuring the FailFast policy errors immediate with no connection available exceptions
            var options = new ConfigurationOptions()
            {
                BacklogPolicy = BacklogPolicy.FailFast,
                AbortOnConnectFail = false,
                ConnectTimeout = 1000,
                ConnectRetry = 2,
                SyncTimeout = 10000,
                KeepAlive = 10000,
                AsyncTimeout = 5000,
                AllowAdmin = true,
                AllowSimulateConnectionFailure = true,
            };
            options.EndPoints.Add(TestConfig.Current.PrimaryServerAndPort);

            await using var conn = await ConnectionMultiplexer.ConnectAsync(options, Writer);

            var db = conn.GetDatabase();
            Log("Test: Initial (connected) ping");
            await db.PingAsync();

            var server = conn.GetServerSnapshot()[0];
            Assert.SkipUnless(server.CanSimulateConnectionFailure, "Skipping because server cannot simulate connection failure");
            var stats = server.GetBridgeStatus(ConnectionType.Interactive);
            stats.BacklogMessagesPending.Should().Be(0); // Everything's normal

            // Fail the connection
            Log("Test: Simulating failure");
            conn.AllowConnect = false;

            server.SimulateConnectionFailure(SimulatedFailureType.All);
            conn.IsConnected.Should().BeFalse();

            // Queue up some commands
            Log("Test: Disconnected pings");
            await Assert.ThrowsAsync<RedisConnectionException>(() => db.PingAsync());

            var disconnectedStats = server.GetBridgeStatus(ConnectionType.Interactive);
            conn.IsConnected.Should().BeFalse();
            disconnectedStats.BacklogMessagesPending.Should().Be(0);

            Log("Test: Allowing reconnect");
            conn.AllowConnect = true;
            Log("Test: Awaiting reconnect");
            await UntilConditionAsync(TimeSpan.FromSeconds(3), () => conn.IsConnected).ForAwait();

            Log("Test: Reconnecting");
            conn.IsConnected.Should().BeTrue();
            server.IsConnected.Should().BeTrue();
            var reconnectedStats = server.GetBridgeStatus(ConnectionType.Interactive);
            reconnectedStats.BacklogMessagesPending.Should().Be(0);

            _ = db.PingAsync();
            _ = db.PingAsync();
            var lastPing = db.PingAsync();

            // For debug, print out the snapshot and server states
            PrintSnapshot(conn);

            Assert.NotNull(conn.SelectServer(Message.Create(-1, CommandFlags.None, RedisCommand.PING)));
            // We should see none queued
            stats.BacklogMessagesPending.Should().Be(0);
            await lastPing;
        }
        finally
        {
            ClearAmbientFailures();
        }
    }

    [Fact]
    [Trait(TestCategories.Category, TestCategories.SimulatedConnectionFailure)]
    public async Task queues_and_flushes_after_reconnecting_async()
    {
        try
        {
            var options = new ConfigurationOptions()
            {
                BacklogPolicy = BacklogPolicy.Default,
                AbortOnConnectFail = false,
                ConnectTimeout = 1000,
                ConnectRetry = 2,
                SyncTimeout = 10000,
                KeepAlive = 10000,
                AsyncTimeout = 5000,
                AllowAdmin = true,
                AllowSimulateConnectionFailure = true,
            };
            options.EndPoints.Add(TestConfig.Current.PrimaryServerAndPort);

            await using var conn = await ConnectionMultiplexer.ConnectAsync(options, Writer);
            Assert.SkipUnless(conn.IsConnected, "no initial connection");
            conn.ErrorMessage += (s, e) => Log($"Error Message {e.EndPoint}: {e.Message}");
            conn.InternalError += (s, e) => Log($"Internal Error {e.EndPoint}: {e.Exception.Message}");
            conn.ConnectionFailed += (s, a) => Log("Disconnected: " + EndPointCollection.ToString(a.EndPoint));
            conn.ConnectionRestored += (s, a) => Log("Reconnected: " + EndPointCollection.ToString(a.EndPoint));

            var db = conn.GetDatabase();
            Log("Test: Initial (connected) ping");
            await db.PingAsync();

            var server = conn.GetServerSnapshot()[0];
            Assert.SkipUnless(server.CanSimulateConnectionFailure, "Skipping because server cannot simulate connection failure");
            var stats = server.GetBridgeStatus(ConnectionType.Interactive);
            stats.BacklogMessagesPending.Should().Be(0); // Everything's normal

            // Fail the connection
            Log("Test: Simulating failure");
            conn.AllowConnect = false;
            server.SimulateConnectionFailure(SimulatedFailureType.All);
            conn.IsConnected.Should().BeFalse();

            // Queue up some commands
            Log("Test: Disconnected pings");
            var ignoredA = db.PingAsync();
            var ignoredB = db.PingAsync();
            var lastPing = db.PingAsync();

            // TODO: Add specific server call
            var disconnectedStats = server.GetBridgeStatus(ConnectionType.Interactive);
            conn.IsConnected.Should().BeFalse();
            (disconnectedStats.BacklogMessagesPending >= 3).Should().BeTrue($"Expected {nameof(disconnectedStats.BacklogMessagesPending)} > 3, got {disconnectedStats.BacklogMessagesPending}");

            Log("Test: Allowing reconnect");
            conn.AllowConnect = true;
            Log("Test: Awaiting reconnect");
            await UntilConditionAsync(TimeSpan.FromSeconds(3), () => conn.IsConnected).ForAwait();

            Log("Test: Checking reconnected 1");
            conn.IsConnected.Should().BeTrue();

            Log("Test: ignoredA Status: " + ignoredA.Status);
            Log("Test: ignoredB Status: " + ignoredB.Status);
            Log("Test: lastPing Status: " + lastPing.Status);
            var afterConnectedStats = server.GetBridgeStatus(ConnectionType.Interactive);
            Log($"Test: BacklogStatus: {afterConnectedStats.BacklogStatus}, BacklogMessagesPending: {afterConnectedStats.BacklogMessagesPending}, IsWriterActive: {afterConnectedStats.IsWriterActive}, MessagesSinceLastHeartbeat: {afterConnectedStats.MessagesSinceLastHeartbeat}, TotalBacklogMessagesQueued: {afterConnectedStats.TotalBacklogMessagesQueued}");

            Log("Test: Awaiting lastPing 1");
            await lastPing;

            Log("Test: Checking reconnected 2");
            conn.IsConnected.Should().BeTrue();
            var reconnectedStats = server.GetBridgeStatus(ConnectionType.Interactive);
            reconnectedStats.BacklogMessagesPending.Should().Be(0);

            Log("Test: Pinging again...");
            _ = db.PingAsync();
            _ = db.PingAsync();
            Log("Test: Last Ping issued");
            lastPing = db.PingAsync();

            // We should see none queued
            Log("Test: BacklogMessagesPending check");
            stats.BacklogMessagesPending.Should().Be(0);
            Log("Test: Awaiting lastPing 2");
            await lastPing;
            Log("Test: Done");
        }
        finally
        {
            ClearAmbientFailures();
        }
    }

    [Fact]
    [Trait(TestCategories.Category, TestCategories.SimulatedConnectionFailure)]
    public async Task queues_and_flushes_after_reconnecting()
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        try
        {
            var options = new ConfigurationOptions()
            {
                BacklogPolicy = BacklogPolicy.Default,
                AbortOnConnectFail = false,
                ConnectTimeout = 1000,
                ConnectRetry = 2,
                SyncTimeout = 10000,
                KeepAlive = 10000,
                AsyncTimeout = 5000,
                AllowAdmin = true,
                AllowSimulateConnectionFailure = true,
            };
            options.EndPoints.Add(TestConfig.Current.PrimaryServerAndPort);

            await using var conn = await ConnectionMultiplexer.ConnectAsync(options, Writer);
            conn.ErrorMessage += (s, e) => Log($"Error Message {e.EndPoint}: {e.Message}");
            conn.InternalError += (s, e) => Log($"Internal Error {e.EndPoint}: {e.Exception.Message}");
            conn.ConnectionFailed += (s, a) => Log("Disconnected: " + EndPointCollection.ToString(a.EndPoint));
            conn.ConnectionRestored += (s, a) => Log("Reconnected: " + EndPointCollection.ToString(a.EndPoint));

            var db = conn.GetDatabase();
            Log("Test: Initial (connected) ping");
            await db.PingAsync();

            var server = conn.GetServerSnapshot()[0];
            Assert.SkipUnless(server.CanSimulateConnectionFailure, "Skipping because server cannot simulate connection failure");
            var stats = server.GetBridgeStatus(ConnectionType.Interactive);
            stats.BacklogMessagesPending.Should().Be(0); // Everything's normal

            // Fail the connection
            Log("Test: Simulating failure");
            conn.AllowConnect = false;
            server.SimulateConnectionFailure(SimulatedFailureType.All);
            conn.IsConnected.Should().BeFalse();

            // Queue up some commands
            Log("Test: Disconnected pings");

            Task[] pings =
            [
                RunBlockingSynchronousWithExtraThreadAsync(() => DisconnectedPings(1)),
                RunBlockingSynchronousWithExtraThreadAsync(() => DisconnectedPings(2)),
                RunBlockingSynchronousWithExtraThreadAsync(() => DisconnectedPings(3)),
            ];
            void DisconnectedPings(int id)
            {
                // No need to delay, we're going to try a disconnected connection immediately so it'll fail...
                Log($"Pinging (disconnected - {id})");
                var result = db.Ping();
                Log($"Pinging (disconnected - {id}) - result: " + result);
            }
            Log("Test: Disconnected pings issued");

            conn.IsConnected.Should().BeFalse();
            // Give the tasks time to queue
            await UntilConditionAsync(TimeSpan.FromSeconds(5), () => server.GetBridgeStatus(ConnectionType.Interactive).BacklogMessagesPending >= 3);

            var disconnectedStats = server.GetBridgeStatus(ConnectionType.Interactive);
            Log($"Test Stats: (BacklogMessagesPending: {disconnectedStats.BacklogMessagesPending}, TotalBacklogMessagesQueued: {disconnectedStats.TotalBacklogMessagesQueued})");
            (disconnectedStats.BacklogMessagesPending >= 3).Should().BeTrue($"Expected {nameof(disconnectedStats.BacklogMessagesPending)} > 3, got {disconnectedStats.BacklogMessagesPending}");

            Log("Test: Allowing reconnect");
            conn.AllowConnect = true;
            Log("Test: Awaiting reconnect");
            await UntilConditionAsync(TimeSpan.FromSeconds(3), () => conn.IsConnected).ForAwait();

            Log("Test: Checking reconnected 1");
            conn.IsConnected.Should().BeTrue();

            var afterConnectedStats = server.GetBridgeStatus(ConnectionType.Interactive);
            Log($"Test: BacklogStatus: {afterConnectedStats.BacklogStatus}, BacklogMessagesPending: {afterConnectedStats.BacklogMessagesPending}, IsWriterActive: {afterConnectedStats.IsWriterActive}, MessagesSinceLastHeartbeat: {afterConnectedStats.MessagesSinceLastHeartbeat}, TotalBacklogMessagesQueued: {afterConnectedStats.TotalBacklogMessagesQueued}");

            Log("Test: Awaiting 3 pings");
            await Task.WhenAll(pings);

            Log("Test: Checking reconnected 2");
            conn.IsConnected.Should().BeTrue();
            var reconnectedStats = server.GetBridgeStatus(ConnectionType.Interactive);
            reconnectedStats.BacklogMessagesPending.Should().Be(0);

            Log("Test: Pinging again...");
            pings[0] = RunBlockingSynchronousWithExtraThreadAsync(() => DisconnectedPings(4));
            pings[1] = RunBlockingSynchronousWithExtraThreadAsync(() => DisconnectedPings(5));
            pings[2] = RunBlockingSynchronousWithExtraThreadAsync(() => DisconnectedPings(6));
            Log("Test: Last Ping queued");

            // We should see none queued
            Log("Test: BacklogMessagesPending check");
            stats.BacklogMessagesPending.Should().Be(0);
            Log("Test: Awaiting 3 more pings");
            await Task.WhenAll(pings);
            Log("Test: Done");
        }
        finally
        {
            ClearAmbientFailures();
        }
    }

    [Fact]
    [Trait(TestCategories.Category, TestCategories.SimulatedConnectionFailure)]
    public async Task queues_and_flushes_after_reconnecting_cluster_async()
    {
        try
        {
            var options = ConfigurationOptions.Parse(TestConfig.Current.ClusterServersAndPorts);
            options.BacklogPolicy = BacklogPolicy.Default;
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 1000;
            options.ConnectRetry = 2;
            options.SyncTimeout = 10000;
            options.KeepAlive = 10000;
            options.AsyncTimeout = 5000;
            options.AllowAdmin = true;
            options.AllowSimulateConnectionFailure = true;

            await using var conn = await ConnectionMultiplexer.ConnectAsync(options, Writer);
            Assert.SkipUnless(conn.IsConnected, "no initial connection");
            conn.ErrorMessage += (s, e) => Log($"Error Message {e.EndPoint}: {e.Message}");
            conn.InternalError += (s, e) => Log($"Internal Error {e.EndPoint}: {e.Exception.Message}");
            conn.ConnectionFailed += (s, a) => Log("Disconnected: " + EndPointCollection.ToString(a.EndPoint));
            conn.ConnectionRestored += (s, a) => Log("Reconnected: " + EndPointCollection.ToString(a.EndPoint));

            var db = conn.GetDatabase();
            Log("Test: Initial (connected) ping");
            await db.PingAsync();

            RedisKey meKey = Me();
            var getMsg = Message.Create(0, CommandFlags.None, RedisCommand.GET, meKey);

            ServerEndPoint? server = null; // Get the server specifically for this message's hash slot
            await UntilConditionAsync(TimeSpan.FromSeconds(10), () => (server = conn.SelectServer(getMsg)) != null);

            Assert.NotNull(server);
            Assert.SkipUnless(server.CanSimulateConnectionFailure, "Skipping because server cannot simulate connection failure");
            var stats = server.GetBridgeStatus(ConnectionType.Interactive);
            stats.BacklogMessagesPending.Should().Be(0); // Everything's normal

            static Task<TimeSpan> PingAsync(ServerEndPoint server, CommandFlags flags = CommandFlags.None)
            {
                var message = ResultProcessor.TimingProcessor.CreateMessage(-1, flags, RedisCommand.PING);

                server.Multiplexer.CheckMessage(message);
                return server.Multiplexer.ExecuteAsyncImpl(message, ResultProcessor.ResponseTimer, null, server);
            }

            // Fail the connection
            Log("Test: Simulating failure");
            conn.AllowConnect = false;
            server.SimulateConnectionFailure(SimulatedFailureType.All);
            server.IsConnected.Should().BeFalse(); // Server isn't connected
            conn.IsConnected.Should().BeTrue(); // ...but the multiplexer is

            // Queue up some commands
            Log("Test: Disconnected pings");
            var ignoredA = PingAsync(server);
            var ignoredB = PingAsync(server);
            var lastPing = PingAsync(server);

            var disconnectedStats = server.GetBridgeStatus(ConnectionType.Interactive);
            server.IsConnected.Should().BeFalse();
            conn.IsConnected.Should().BeTrue();
            (disconnectedStats.BacklogMessagesPending >= 3).Should().BeTrue($"Expected {nameof(disconnectedStats.BacklogMessagesPending)} > 3, got {disconnectedStats.BacklogMessagesPending}");

            Log("Test: Allowing reconnect");
            conn.AllowConnect = true;
            Log("Test: Awaiting reconnect");
            await UntilConditionAsync(TimeSpan.FromSeconds(3), () => server.IsConnected).ForAwait();

            Log("Test: Checking reconnected 1");
            server.IsConnected.Should().BeTrue();
            conn.IsConnected.Should().BeTrue();

            Log("Test: ignoredA Status: " + ignoredA.Status);
            Log("Test: ignoredB Status: " + ignoredB.Status);
            Log("Test: lastPing Status: " + lastPing.Status);
            var afterConnectedStats = server.GetBridgeStatus(ConnectionType.Interactive);
            Log($"Test: BacklogStatus: {afterConnectedStats.BacklogStatus}, BacklogMessagesPending: {afterConnectedStats.BacklogMessagesPending}, IsWriterActive: {afterConnectedStats.IsWriterActive}, MessagesSinceLastHeartbeat: {afterConnectedStats.MessagesSinceLastHeartbeat}, TotalBacklogMessagesQueued: {afterConnectedStats.TotalBacklogMessagesQueued}");

            Log("Test: Awaiting lastPing 1");
            await lastPing;

            Log("Test: Checking reconnected 2");
            server.IsConnected.Should().BeTrue();
            conn.IsConnected.Should().BeTrue();
            var reconnectedStats = server.GetBridgeStatus(ConnectionType.Interactive);
            reconnectedStats.BacklogMessagesPending.Should().Be(0);

            Log("Test: Pinging again...");
            _ = PingAsync(server);
            _ = PingAsync(server);
            Log("Test: Last Ping issued");
            lastPing = PingAsync(server);

            // We should see none queued
            Log("Test: BacklogMessagesPending check");
            stats.BacklogMessagesPending.Should().Be(0);
            Log("Test: Awaiting lastPing 2");
            await lastPing;
            Log("Test: Done");
        }
        finally
        {
            ClearAmbientFailures();
        }
    }

    [Fact]
    [Trait(TestCategories.Category, TestCategories.SimulatedConnectionFailure)]
    public async Task total_outstanding_includes_backlog_queue()
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        try
        {
            var options = new ConfigurationOptions()
            {
                BacklogPolicy = BacklogPolicy.Default,
                AbortOnConnectFail = false,
                ConnectTimeout = 1000,
                ConnectRetry = 2,
                SyncTimeout = 10000,
                KeepAlive = 10000,
                AsyncTimeout = 5000,
                AllowAdmin = true,
                AllowSimulateConnectionFailure = true,
            };
            options.EndPoints.Add(TestConfig.Current.PrimaryServerAndPort);

            using var conn = await ConnectionMultiplexer.ConnectAsync(options, Writer);
            var db = conn.GetDatabase();
            Log("Test: Initial (connected) ping");
            await db.PingAsync();

            var server = conn.GetServerSnapshot()[0];
            Assert.SkipUnless(server.CanSimulateConnectionFailure, "Skipping because server cannot simulate connection failure");

            // Verify TotalOutstanding is 0 when connected and idle
            Log("Test: asserting connected counters");
            var connectedServerCounters = server.GetCounters();
            var connectedConnCounters = conn.GetCounters();
            connectedServerCounters.Interactive.TotalOutstanding.Should().Be(0);
            connectedConnCounters.TotalOutstanding.Should().Be(0);

            Log("Test: Simulating failure");
            conn.AllowConnect = false;
            server.SimulateConnectionFailure(SimulatedFailureType.All);

            // Queue up some commands
            Log("Test: Disconnected pings");
            _ = db.PingAsync();
            _ = db.PingAsync();
            var lastPing = db.PingAsync();

            Log("Test: asserting disconnected counters");
            var disconnectedServerCounters = server.GetCounters();
            var disconnectedConnCounters = conn.GetCounters();
            (disconnectedServerCounters.Interactive.PendingUnsentItems >= 3).Should().BeTrue($"Expected PendingUnsentItems >= 3, got {disconnectedServerCounters.Interactive.PendingUnsentItems}");
            (disconnectedConnCounters.TotalOutstanding >= 3).Should().BeTrue($"Expected TotalOutstanding >= 3, got {disconnectedServerCounters.Interactive.TotalOutstanding}");

            Log("Test: Awaiting reconnect");
            conn.AllowConnect = true;
            await UntilConditionAsync(TimeSpan.FromSeconds(3), () => conn.IsConnected).ForAwait();

            Log("Test: Awaiting lastPing");
            await lastPing;

            Log("Test: Checking reconnected");
            conn.IsConnected.Should().BeTrue();

            Log("Test: asserting reconnected counters");
            var reconnectedServerCounters = server.GetCounters();
            var reconnectedConnCounters = conn.GetCounters();
            reconnectedServerCounters.Interactive.PendingUnsentItems.Should().Be(0);
            reconnectedConnCounters.TotalOutstanding.Should().Be(0);
            Log("Test: Done");
        }
        finally
        {
            ClearAmbientFailures();
        }
    }
}
