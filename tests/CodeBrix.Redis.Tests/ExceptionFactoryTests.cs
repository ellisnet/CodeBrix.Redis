using System;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class ExceptionFactoryTests(ITestOutputHelper output, InProcServerFixture fixture) : TestBase(output, fixture)
{
    [Fact]
    public async Task null_last_exception()
    {
        await using var conn = Create(keepAlive: 1, connectTimeout: 10000, allowAdmin: true);

        conn.GetDatabase();
        conn.GetServerSnapshot()[0].LastException.Should().BeNull();
        var ex = ExceptionFactory.NoConnectionAvailable(conn.UnderlyingMultiplexer, null, null);
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void can_get_version()
    {
        var libVer = Utils.GetLibVersion();
        //was previously: @"[2-3]\.[0-9]+\.[0-9]+(\.[0-9]+)?" - upstream ships 2.x/3.x. This package
        //uses the CodeBrix date-stamped scheme, 1.<years since 2026>.<day of year>.<minute of day>,
        //so the shape the test checks - a dotted numeric version that GetLibVersion can read - is the
        //same, with this repository's major range.
        libVer.Should().MatchRegex(@"[0-9]+\.[0-9]+\.[0-9]+(\.[0-9]+)?");
    }

#if DEBUG
    [Fact]
    [Trait(TestCategories.Category, TestCategories.SimulatedConnectionFailure)]
    public async Task multiple_endpoints_throw_connection_exception()
    {
        try
        {
            await using var conn = Create(keepAlive: 1, connectTimeout: 10000, allowAdmin: true, allowSimulateConnectionFailure: true);

            conn.GetDatabase();
            conn.AllowConnect = false;

            foreach (var endpoint in conn.GetEndPoints())
            {
                var server = conn.GetServer(endpoint);
                Assert.SkipUnless(server.CanSimulateConnectionFailure(), "Skipping because server cannot simulate connection failure");
                server.SimulateConnectionFailure(SimulatedFailureType.All);
            }

            var ex = ExceptionFactory.NoConnectionAvailable(conn.UnderlyingMultiplexer, null, null);
            var outer = Assert.IsType<RedisConnectionException>(ex);
            outer.FailureType.Should().Be(ConnectionFailureType.UnableToResolvePhysicalConnection);
            var inner = Assert.IsType<RedisConnectionException>(outer.InnerException);
            (inner.FailureType == ConnectionFailureType.SocketFailure
                     || inner.FailureType == ConnectionFailureType.InternalFailure).Should().BeTrue();
        }
        finally
        {
            ClearAmbientFailures();
        }
    }
#endif

    [Fact]
    [Trait(TestCategories.Category, TestCategories.SimulatedConnectionFailure)]
    public async Task server_takes_precendence_over_snapshot()
    {
        try
        {
            await using var conn = Create(keepAlive: 1, connectTimeout: 10000, allowAdmin: true, backlogPolicy: BacklogPolicy.FailFast, allowSimulateConnectionFailure: true);

            conn.GetDatabase();
            conn.AllowConnect = false;

            var server = conn.GetServer(conn.GetEndPoints()[0]);
            Assert.SkipUnless(server.CanSimulateConnectionFailure(), "Skipping because server cannot simulate connection failure");
            server.SimulateConnectionFailure(SimulatedFailureType.All);

            var ex = ExceptionFactory.NoConnectionAvailable(conn.UnderlyingMultiplexer, null, conn.GetServerSnapshot()[0]);
            ex.Should().BeOfType<RedisConnectionException>();
            ex.InnerException.Should().BeOfType<RedisConnectionException>();
            conn.GetServerSnapshot()[0].LastException.Should().Be(ex.InnerException);
        }
        finally
        {
            ClearAmbientFailures();
        }
    }

    [Fact]
    public async Task null_inner_exception_for_multiple_endpoints_with_no_last_exception()
    {
        try
        {
            await using var conn = Create(keepAlive: 1, connectTimeout: 10000, allowAdmin: true);

            conn.GetDatabase();
            conn.AllowConnect = false;
            var ex = ExceptionFactory.NoConnectionAvailable(conn.UnderlyingMultiplexer, null, null);
            ex.Should().BeOfType<RedisConnectionException>();
            ex.InnerException.Should().BeNull();
        }
        finally
        {
            ClearAmbientFailures();
        }
    }

    [Fact]
    public async Task timeout_exception()
    {
        try
        {
            await using var conn = Create(keepAlive: 1, connectTimeout: 10000, allowAdmin: true, shared: false);

            var server = GetServer(conn);
            conn.AllowConnect = false;
            var msg = Message.Create(-1, CommandFlags.None, RedisCommand.PING);
            var rawEx = ExceptionFactory.Timeout(conn.UnderlyingMultiplexer, "Test Timeout", msg, new ServerEndPoint(conn.UnderlyingMultiplexer, server.EndPoint));
            var ex = Assert.IsType<RedisTimeoutException>(rawEx);
            Log("Exception: " + ex.Message);

            // Example format: "Test Timeout, command=PING, inst: 0, qu: 0, qs: 0, aw: False, in: 0, in-pipe: 0, out-pipe: 0, last-in: 0, cur-in: 0, serverEndpoint: 127.0.0.1:6379, mgr: 10 of 10 available, clientName: TimeoutException, IOCP: (Busy=0,Free=1000,Min=8,Max=1000), WORKER: (Busy=2,Free=2045,Min=8,Max=2047), v: 2.1.0 (Please take a look at this article for some common client-side issues that can cause timeouts: https://seredis.dev/Timeouts)";
            ex.Message.Should().StartWith("Test Timeout, command=PING");
            ex.Message.Should().Contain("clientName: " + nameof(timeout_exception));
            // Ensure our pipe numbers are in place
            ex.Message.Should().Contain("inst: 0, qu: 0, qs: 0, aw: False, bw: Inactive, in: 0, in-pipe: 0, out-pipe: 0, last-in: 0, cur-in: 0");
            ex.Message.Should().Contain("mc: 1/1/0");
            ex.Message.Should().Contain("serverEndpoint: " + server.EndPoint);
            ex.Message.Should().Contain("IOCP: ");
            ex.Message.Should().Contain("WORKER: ");
            ex.Message.Should().Contain("sync-ops: ");
            ex.Message.Should().Contain("async-ops: ");
            ex.Message.Should().Contain("conn-sec: n/a");
            ex.Message.Should().Contain("aoc: 0");
            // ...POOL: (Threads=33,QueuedItems=0,CompletedItems=5547,Timers=60)...
            ex.Message.Should().Contain("POOL: ");
            ex.Message.Should().Contain("Threads=");
            ex.Message.Should().Contain("QueuedItems=");
            ex.Message.Should().Contain("CompletedItems=");
            ex.Message.Should().Contain("Timers=");
            ex.Message.Should().NotContain("Unspecified/");
            ex.Message.Should().EndWith(" (Please take a look at this article for some common client-side issues that can cause timeouts: https://seredis.dev/Timeouts)");
            ex.InnerException.Should().BeNull();
        }
        finally
        {
            ClearAmbientFailures();
        }
    }

    [Theory]
    [InlineData(false, 0, 0, true, "Connection to Redis never succeeded (attempts: 0 - connection likely in-progress), unable to service operation: PING")]
    [InlineData(false, 1, 0, true, "Connection to Redis never succeeded (attempts: 1 - connection likely in-progress), unable to service operation: PING")]
    [InlineData(false, 12, 0, true, "Connection to Redis never succeeded (attempts: 12 - check your config), unable to service operation: PING")]
    [InlineData(false, 0, 0, false, "Connection to Redis never succeeded (attempts: 0 - connection likely in-progress), unable to service operation: PING")]
    [InlineData(false, 1, 0, false, "Connection to Redis never succeeded (attempts: 1 - connection likely in-progress), unable to service operation: PING")]
    [InlineData(false, 12, 0, false, "Connection to Redis never succeeded (attempts: 12 - check your config), unable to service operation: PING")]
    [InlineData(true, 0, 0, true, "No connection is active/available to service this operation: PING")]
    [InlineData(true, 1, 0, true, "No connection is active/available to service this operation: PING")]
    [InlineData(true, 12, 0, true, "No connection is active/available to service this operation: PING")]
    public async Task no_connection_exception(bool abortOnConnect, int connCount, int completeCount, bool hasDetail, string messageStart)
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        try
        {
            var options = new ConfigurationOptions()
            {
                AbortOnConnectFail = abortOnConnect,
                BacklogPolicy = BacklogPolicy.FailFast,
                ConnectTimeout = 1000,
                SyncTimeout = 500,
                KeepAlive = 5000,
            };

            ConnectionMultiplexer conn;
            if (abortOnConnect)
            {
                options.EndPoints.Add(TestConfig.Current.PrimaryServerAndPort);
                conn = ConnectionMultiplexer.Connect(options, Writer);
            }
            else
            {
                options.EndPoints.Add($"doesnot.exist.{Guid.NewGuid():N}:6379");
                conn = ConnectionMultiplexer.Connect(options, Writer);
            }

            await using (conn)
            {
                var server = conn.GetServer(conn.GetEndPoints()[0]);
                conn.AllowConnect = false;
                conn._connectAttemptCount = connCount;
                conn._connectCompletedCount = completeCount;
                options.IncludeDetailInExceptions = hasDetail;
                options.IncludePerformanceCountersInExceptions = hasDetail;

                var msg = Message.Create(-1, CommandFlags.None, RedisCommand.PING);
                var rawEx = ExceptionFactory.NoConnectionAvailable(conn, msg, new ServerEndPoint(conn, server.EndPoint));
                var ex = Assert.IsType<RedisConnectionException>(rawEx);
                Log("Exception: " + ex.Message);

                // Example format: "Exception: No connection is active/available to service this operation: PING, inst: 0, qu: 0, qs: 0, aw: False, in: 0, in-pipe: 0, out-pipe: 0, last-in: 0, cur-in: 0, serverEndpoint: 127.0.0.1:6379, mc: 1/1/0, mgr: 10 of 10 available, clientName: NoConnectionException, IOCP: (Busy=0,Free=1000,Min=8,Max=1000), WORKER: (Busy=2,Free=2045,Min=8,Max=2047), Local-CPU: 100%, v: 2.1.0.5";
                ex.Message.Should().StartWith(messageStart);

                // Ensure our pipe numbers are in place if they should be
                if (hasDetail)
                {
                    ex.Message.Should().Contain("inst: 0, qu: 0, qs: 0, aw: False, bw: Inactive, in: 0, in-pipe: 0, out-pipe: 0, last-in: 0, cur-in: 0");
                    ex.Message.Should().Contain($"mc: {connCount}/{completeCount}/0");
                    ex.Message.Should().Contain("serverEndpoint: " + server.EndPoint.ToString()?.Replace("Unspecified/", ""));
                }
                else
                {
                    ex.Message.Should().NotContain("inst: 0, qu: 0, qs: 0, aw: False, bw: Inactive, in: 0, in-pipe: 0, out-pipe: 0, last-in: 0, cur-in: 0");
                    ex.Message.Should().NotContain($"mc: {connCount}/{completeCount}/0");
                    ex.Message.Should().NotContain("serverEndpoint: " + server.EndPoint.ToString()?.Replace("Unspecified/", ""));
                }
                ex.Message.Should().NotContain("Unspecified/");
            }
        }
        finally
        {
            ClearAmbientFailures();
        }
    }

    [Fact]
    public async Task no_connection_primary_only_exception()
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        await using var conn = await ConnectionMultiplexer.ConnectAsync(TestConfig.Current.ReplicaServerAndPort, Writer);

        var msg = Message.Create(0, CommandFlags.None, RedisCommand.SET, (RedisKey)Me(), (RedisValue)"test");
        msg.IsPrimaryOnly().Should().BeTrue();
        var rawEx = ExceptionFactory.NoConnectionAvailable(conn, msg, null);
        var ex = Assert.IsType<RedisConnectionException>(rawEx);
        Log("Exception: " + ex.Message);

        // Ensure a primary-only operation like SET gives the additional context
        ex.Message.Should().StartWith("No connection (requires writable - not eligible for replica) is active/available to service this operation: SET");
    }

    [Theory]
    [InlineData(true, ConnectionFailureType.ProtocolFailure, "ProtocolFailure on [0]:GET myKey (StringProcessor), my annotation")]
    [InlineData(true, ConnectionFailureType.ConnectionDisposed, "ConnectionDisposed on [0]:GET myKey (StringProcessor), my annotation")]
    [InlineData(false, ConnectionFailureType.ProtocolFailure, "ProtocolFailure on [0]:GET (StringProcessor), my annotation")]
    [InlineData(false, ConnectionFailureType.ConnectionDisposed, "ConnectionDisposed on [0]:GET (StringProcessor), my annotation")]
    public async Task message_fail(bool includeDetail, ConnectionFailureType failType, string messageStart)
    {
        //Arrange
        await using var conn = Create(shared: false);
        conn.RawConfig.IncludeDetailInExceptions = includeDetail;
        var message = Message.Create(0, CommandFlags.None, RedisCommand.GET, (RedisKey)"myKey");
        var resultBox = SimpleResultBox<string>.Create();
        message.SetSource(ResultProcessor.String, resultBox);
        message.Fail(failType, null, "my annotation", conn.UnderlyingMultiplexer);

        //Act
        resultBox.GetResult(out var ex);

        //Assert
        Assert.NotNull(ex);
        ex.Message.Should().StartWith(messageStart);
    }
}
