using System;
using System.Threading.Tasks;
using CodeBrix.Redis.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class AbortOnConnectFailTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task never_ever_connected_no_backlog_throws_connection_not_available_sync()
    {
        await using var conn = GetFailFastConn();
        var db = conn.GetDatabase();
        var key = Me();

        // No connection is active/available to service this operation: GET 6.0.18AbortOnConnectFailTests-NeverEverConnectedNoBacklogThrowsConnectionNotAvailableSync; UnableToConnect on doesnot.exist.d4d1424806204b68b047954b1db3411d:6379/Interactive, Initializing/NotStarted, last: NONE, origin: BeginConnectAsync, outstanding: 0, last-read: 0s ago, last-write: 0s ago, keep-alive: 100s, state: Connecting, mgr: 4 of 10 available, last-heartbeat: never, global: 0s ago, v: 2.6.120.51136, mc: 1/1/0, mgr: 5 of 10 available, clientName: CRAVERTOP7(CodeBrix.Redis-v2.6.120.51136), IOCP: (Busy=0,Free=1000,Min=16,Max=1000), WORKER: (Busy=3,Free=32764,Min=16,Max=32767), POOL: (Threads=25,QueuedItems=0,CompletedItems=1066,Timers=46), v: 2.6.120.51136
        var ex = Assert.Throws<RedisConnectionException>(() => db.StringGet(key));
        Log("Exception: " + ex.Message);
        ex.Message.Should().Contain("No connection is active/available to service this operation");
    }

    [Fact]
    public async Task never_ever_connected_no_backlog_throws_connection_not_available_async()
    {
        await using var conn = GetFailFastConn();
        var db = conn.GetDatabase();
        var key = Me();

        // No connection is active/available to service this operation: GET 6.0.18AbortOnConnectFailTests-NeverEverConnectedNoBacklogThrowsConnectionNotAvailableSync; UnableToConnect on doesnot.exist.d4d1424806204b68b047954b1db3411d:6379/Interactive, Initializing/NotStarted, last: NONE, origin: BeginConnectAsync, outstanding: 0, last-read: 0s ago, last-write: 0s ago, keep-alive: 100s, state: Connecting, mgr: 4 of 10 available, last-heartbeat: never, global: 0s ago, v: 2.6.120.51136, mc: 1/1/0, mgr: 5 of 10 available, clientName: CRAVERTOP7(CodeBrix.Redis-v2.6.120.51136), IOCP: (Busy=0,Free=1000,Min=16,Max=1000), WORKER: (Busy=3,Free=32764,Min=16,Max=32767), POOL: (Threads=25,QueuedItems=0,CompletedItems=1066,Timers=46), v: 2.6.120.51136
        var ex = await Assert.ThrowsAsync<RedisConnectionException>(() => db.StringGetAsync(key));
        Log("Exception: " + ex.Message);
        ex.Message.Should().Contain("No connection is active/available to service this operation");
    }

    [Fact]
    [Trait(TestCategories.Category, TestCategories.SimulatedConnectionFailure)]
    public async Task disconnect_and_reconnect_throws_connection_exception_sync()
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        await using var conn = GetWorkingBacklogConn();

        var db = conn.GetDatabase();
        var key = Me();
        await db.PingAsync(); // Doesn't throw - we're connected

        // Disconnect and don't allow re-connection
        conn.AllowConnect = false;
        var server = conn.GetServerSnapshot()[0];
        Assert.SkipUnless(server.CanSimulateConnectionFailure, "Skipping because server cannot simulate connection failure");
        server.SimulateConnectionFailure(SimulatedFailureType.All);

        // Exception: The message timed out in the backlog attempting to send because no connection became available (400ms) - Last Connection Exception: SocketFailure (InputReaderCompleted, last-recv: 7) on 127.0.0.1:6379/Interactive, Idle/ReadAsync, last: PING, origin: SimulateConnectionFailure, outstanding: 0, last-read: 0s ago, last-write: 0s ago, keep-alive: 100s, state: ConnectedEstablished, mgr: 10 of 10 available, in: 0, in-pipe: 0, out-pipe: 0, last-heartbeat: never, last-mbeat: 0s ago, global: 0s ago, v: 2.6.120.51136, command=PING, timeout: 100, inst: 13, qu: 1, qs: 0, aw: False, bw: Inactive, last-in: 0, cur-in: 0, sync-ops: 2, async-ops: 0, serverEndpoint: 127.0.0.1:6379, conn-sec: n/a, aoc: 0, mc: 1/1/0, mgr: 10 of 10 available, clientName: CRAVERTOP7(CodeBrix.Redis-v2.6.120.51136), IOCP: (Busy=0,Free=1000,Min=16,Max=1000), WORKER: (Busy=2,Free=32765,Min=16,Max=32767), POOL: (Threads=33,QueuedItems=0,CompletedItems=6237,Timers=39), v: 2.6.120.51136 (Please take a look at this article for some common client-side issues that can cause timeouts: https://seredis.dev/Timeouts)
        var ex = Assert.ThrowsAny<Exception>(() => db.Ping());
        Log("Exception: " + ex.Message);
        (ex is RedisConnectionException or RedisTimeoutException).Should().BeTrue();
        ex.Message.Should().StartWith("The message timed out in the backlog attempting to send because no connection became available (1000ms) - Last Connection Exception: ");
        ex.InnerException.Should().NotBeNull();
        var iex = Assert.IsType<RedisConnectionException>(ex.InnerException);
        ex.Message.Should().Contain(iex.Message);
    }

    [Fact]
    [Trait(TestCategories.Category, TestCategories.SimulatedConnectionFailure)]
    public async Task disconnect_and_no_reconnect_throws_connection_exception_async()
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        await using var conn = GetWorkingBacklogConn();

        var db = conn.GetDatabase();
        var key = Me();
        await db.PingAsync(); // Doesn't throw - we're connected

        // Disconnect and don't allow re-connection
        conn.AllowConnect = false;
        var server = conn.GetServerSnapshot()[0];
        Assert.SkipUnless(server.CanSimulateConnectionFailure, "Skipping because server cannot simulate connection failure");
        server.SimulateConnectionFailure(SimulatedFailureType.All);

        // Exception: The message timed out in the backlog attempting to send because no connection became available (400ms) - Last Connection Exception: SocketFailure (InputReaderCompleted, last-recv: 7) on 127.0.0.1:6379/Interactive, Idle/ReadAsync, last: PING, origin: SimulateConnectionFailure, outstanding: 0, last-read: 0s ago, last-write: 0s ago, keep-alive: 100s, state: ConnectedEstablished, mgr: 8 of 10 available, in: 0, in-pipe: 0, out-pipe: 0, last-heartbeat: never, last-mbeat: 0s ago, global: 0s ago, v: 2.6.120.51136, command=PING, timeout: 100, inst: 0, qu: 0, qs: 0, aw: False, bw: CheckingForTimeout, last-in: 0, cur-in: 0, sync-ops: 1, async-ops: 1, serverEndpoint: 127.0.0.1:6379, conn-sec: n/a, aoc: 0, mc: 1/1/0, mgr: 8 of 10 available, clientName: CRAVERTOP7(CodeBrix.Redis-v2.6.120.51136), IOCP: (Busy=0,Free=1000,Min=16,Max=1000), WORKER: (Busy=6,Free=32761,Min=16,Max=32767), POOL: (Threads=33,QueuedItems=0,CompletedItems=5547,Timers=60), v: 2.6.120.51136 (Please take a look at this article for some common client-side issues that can cause timeouts: https://seredis.dev/Timeouts)
        var ex = await Assert.ThrowsAsync<RedisConnectionException>(() => db.PingAsync());
        Log("Exception: " + ex.Message);
        ex.Message.Should().StartWith("The message timed out in the backlog attempting to send because no connection became available (1000ms) - Last Connection Exception: ");
        ex.InnerException.Should().NotBeNull();
        var iex = Assert.IsType<RedisConnectionException>(ex.InnerException);
        ex.Message.Should().Contain(iex.Message);
    }

    private ConnectionMultiplexer GetFailFastConn() =>
        ConnectionMultiplexer.Connect(GetOptions(BacklogPolicy.FailFast, duration: 400, connectTimeout: 500).Apply(o => o.EndPoints.Add($"doesnot.exist.{Guid.NewGuid():N}:6379")), Writer);

    private ConnectionMultiplexer GetWorkingBacklogConn() =>
        ConnectionMultiplexer.Connect(GetOptions(BacklogPolicy.Default, allowSimulateConnectionFailure: true).Apply(o => o.EndPoints.Add(GetConfiguration())), Writer);

    private static ConfigurationOptions GetOptions(BacklogPolicy policy, int duration = 1000, int connectTimeout = 2000, bool allowSimulateConnectionFailure = false) => new ConfigurationOptions()
    {
        AbortOnConnectFail = false,
        BacklogPolicy = policy,
        ConnectTimeout = connectTimeout,
        SyncTimeout = duration,
        KeepAlive = duration,
        AllowAdmin = true,
        AllowSimulateConnectionFailure = allowSimulateConnectionFailure,
    }.WithoutSubscriptions();
}
