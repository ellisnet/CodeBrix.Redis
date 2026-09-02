using System;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class ConnectCustomConfigTests(ITestOutputHelper output) : TestBase(output)
{
    // So we're triggering tiebreakers here
    protected override string GetConfiguration() => TestConfig.Current.PrimaryServerAndPort + "," + TestConfig.Current.ReplicaServerAndPort;

    [Theory]
    [InlineData("config")]
    [InlineData("info")]
    [InlineData("get")]
    [InlineData("config,get")]
    [InlineData("info,get")]
    [InlineData("config,info,get")]
    public async Task disabled_commands_still_connect(string disabledCommands)
    {
        //Arrange
        await using var conn = Create(allowAdmin: true, disabledCommands: disabledCommands.Split(','), log: Writer);
        var db = conn.GetDatabase();

        //Act
        await db.PingAsync();

        //Assert
        db.IsConnected(default(RedisKey)).Should().BeTrue();
    }

    [Theory]
    [InlineData("config")]
    [InlineData("info")]
    [InlineData("get")]
    [InlineData("cluster")]
    [InlineData("config,get")]
    [InlineData("info,get")]
    [InlineData("config,info,get")]
    [InlineData("config,info,get,cluster")]
    public async Task disabled_commands_still_connect_cluster(string disabledCommands)
    {
        // passes the cluster configuration directly rather than via GetConfiguration(), so it needs
        // its own guard to skip promptly when the cluster is not running
        Skip.IfNoCluster();
        await using var conn = Create(allowAdmin: true, configuration: TestConfig.Current.ClusterServersAndPorts, disabledCommands: disabledCommands.Split(','), log: Writer);

        var db = conn.GetDatabase();
        await db.PingAsync();
        db.IsConnected(default(RedisKey)).Should().BeTrue();
    }

    [Fact]
    public async Task tie_breaker_intact()
    {
        await using var conn = Create(allowAdmin: true, log: Writer);

        var tiebreaker = conn.GetDatabase().StringGet(conn.RawConfig.TieBreaker);
        Log($"Tiebreaker: {tiebreaker}");

        foreach (var server in conn.GetServerSnapshot())
        {
            server.TieBreakerResult.Should().Be(tiebreaker);
        }
    }

    [Fact]
    public async Task tie_breaker_skips()
    {
        await using var conn = Create(allowAdmin: true, disabledCommands: ["get"], log: Writer);
        Assert.Throws<RedisCommandException>(() => conn.GetDatabase().StringGet(conn.RawConfig.TieBreaker));

        foreach (var server in conn.GetServerSnapshot())
        {
            server.IsConnected.Should().BeTrue();
            server.TieBreakerResult.Should().BeNull();
        }
    }

    [Fact]
    public async Task tiebreaker_incorrect_type()
    {
        var tiebreakerKey = Me();
        await using var fubarConn = Create(allowAdmin: true, log: Writer);
        // Store something nonsensical in the tiebreaker key:
        fubarConn.GetDatabase().HashSet(tiebreakerKey, "foo", "bar");

        // Ensure the next connection getting an invalid type still connects
        await using var conn = Create(allowAdmin: true, tieBreaker: tiebreakerKey, log: Writer);

        var db = conn.GetDatabase();
        await db.PingAsync();
        db.IsConnected(default(RedisKey)).Should().BeTrue();

        var ex = Assert.Throws<RedisServerException>(() => db.StringGet(tiebreakerKey));
        ex.Message.Should().Contain("WRONGTYPE");
    }

    [Theory]
    [InlineData(true, 2, 15)]
    [InlineData(false, 0, 0)]
    public async Task heartbeat_consistency_check_pings_async(bool enableConsistencyChecks, int minExpected, int maxExpected)
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        var options = new ConfigurationOptions()
        {
            HeartbeatConsistencyChecks = enableConsistencyChecks,
            HeartbeatInterval = TimeSpan.FromMilliseconds(100),
        };
        options.EndPoints.Add(TestConfig.Current.PrimaryServerAndPort);

        await using var conn = await ConnectionMultiplexer.ConnectAsync(options, Writer);

        var db = conn.GetDatabase();
        await db.PingAsync();
        db.IsConnected(default).Should().BeTrue();

        var preCount = conn.OperationCount;
        Log("OperationCount (pre-delay): " + preCount);

        // Allow several heartbeats to happen, but don't need to be strict here
        // e.g. allow thread pool starvation flex with the test suite's load (just check for a few)
        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        var postCount = conn.OperationCount;
        Log("OperationCount (post-delay): " + postCount);

        var opCount = postCount - preCount;
        Log("OperationCount (diff): " + opCount);

        (minExpected <= opCount && opCount >= minExpected).Should().BeTrue($"Expected opcount ({opCount}) between {minExpected}-{maxExpected}");
    }
}
