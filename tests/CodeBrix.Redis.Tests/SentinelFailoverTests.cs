using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[Collection(NonParallelCollection.Name)]
public class SentinelFailoverTests(ITestOutputHelper output) : SentinelBase(output)
{
    [Fact]
    public async Task managed_primary_connection_end_to_end_with_failover_test()
    {
        Skip.UnlessLongRunning();
        var connectionString = $"{TestConfig.Current.SentinelServer}:{TestConfig.Current.SentinelPortA},serviceName={ServiceOptions.ServiceName},allowAdmin=true";
        await using var conn = await ConnectionMultiplexer.ConnectAsync(connectionString);

        conn.ConfigurationChanged += (s, e) => Log($"Configuration changed: {e.EndPoint}");

        var sub = conn.GetSubscriber();
        sub.Subscribe(RedisChannel.Pattern("*"), (channel, message) => Log($"Sub: {channel}, message:{message}"));

        var db = conn.GetDatabase();
        await db.PingAsync();

        var endpoints = conn.GetEndPoints();
        endpoints.Length.Should().Be(2);

        var servers = endpoints.Select(e => conn.GetServer(e)).ToArray();
        servers.Length.Should().Be(2);

        var primary = servers.FirstOrDefault(s => !s.IsReplica);
        Assert.NotNull(primary);
        var replica = servers.FirstOrDefault(s => s.IsReplica);
        Assert.NotNull(replica);
        replica.EndPoint.ToString().Should().NotBe(primary.EndPoint.ToString());

        // Set string value on current primary
        var expected = DateTime.Now.Ticks.ToString();
        Log("Tick Key: " + expected);
        var key = Me();
        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);
        await db.StringSetAsync(key, expected);

        var value = await db.StringGetAsync(key);
        value.Should().Be(expected);

        Log("Waiting for first replication check...");
        // force read from replica, replication has some lag
        await WaitForReplicationAsync(servers[0]).ForAwait();
        value = await db.StringGetAsync(key, CommandFlags.DemandReplica);
        value.Should().Be(expected);

        Log("Waiting for ready pre-failover...");
        await WaitForReadyAsync();

        // capture current replica
        var replicas = SentinelServerA.SentinelGetReplicaAddresses(ServiceName);

        Log("Starting failover...");
        var sw = Stopwatch.StartNew();
        SentinelServerA.SentinelFailover(ServiceName);

        // There's no point in doing much for 10 seconds - this is a built-in delay of how Sentinel works.
        // The actual completion invoking the replication of the former primary is handled via
        // https://github.com/redis/redis/blob/f233c4c59d24828c77eb1118f837eaee14695f7f/src/sentinel.c#L4799-L4808
        // ...which is invoked by INFO polls every 10 seconds (https://github.com/redis/redis/blob/f233c4c59d24828c77eb1118f837eaee14695f7f/src/sentinel.c#L81)
        // ...which is calling https://github.com/redis/redis/blob/f233c4c59d24828c77eb1118f837eaee14695f7f/src/sentinel.c#L2666
        // However, the quicker iteration on INFO during an o_down does not apply here: https://github.com/redis/redis/blob/f233c4c59d24828c77eb1118f837eaee14695f7f/src/sentinel.c#L3089-L3104
        // So...we're waiting 10 seconds, no matter what. Might as well just idle to be more stable.
        await Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // wait until the replica becomes the primary
        Log("Waiting for ready post-failover...");
        await WaitForReadyAsync(expectedPrimary: replicas[0]);
        Log($"Time to failover: {sw.Elapsed}");

        endpoints = conn.GetEndPoints();
        endpoints.Length.Should().Be(2);

        servers = endpoints.Select(e => conn.GetServer(e)).ToArray();
        servers.Length.Should().Be(2);

        var newPrimary = servers.FirstOrDefault(s => !s.IsReplica);
        Assert.NotNull(newPrimary);
        newPrimary.EndPoint.ToString().Should().Be(replica.EndPoint.ToString());
        var newReplica = servers.FirstOrDefault(s => s.IsReplica);
        Assert.NotNull(newReplica);
        newReplica.EndPoint.ToString().Should().Be(primary.EndPoint.ToString());
        replica.EndPoint.ToString().Should().NotBe(primary.EndPoint.ToString());

        value = await db.StringGetAsync(key);
        value.Should().Be(expected);

        Log("Waiting for second replication check...");
        // force read from replica, replication has some lag
        await WaitForReplicationAsync(newPrimary).ForAwait();
        value = await db.StringGetAsync(key, CommandFlags.DemandReplica);
        value.Should().Be(expected);
    }
}
