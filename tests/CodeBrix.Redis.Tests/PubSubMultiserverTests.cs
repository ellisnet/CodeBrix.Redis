using System;
using System.Threading;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[RunPerProtocol]
public class PubSubMultiserverTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    protected override string GetConfiguration() => GetClusterConfiguration();

    [Fact]
    public async Task channel_sharding()
    {
        await using var conn = Create(channelPrefix: Me());

        var defaultSlot = conn.ServerSelectionStrategy.HashSlot(default(RedisChannel));
        var slot1 = conn.ServerSelectionStrategy.HashSlot(RedisChannel.Literal("hey"));
        var slot2 = conn.ServerSelectionStrategy.HashSlot(RedisChannel.Literal("hey2"));

        slot1.Should().NotBe(defaultSlot);
        slot1.Should().NotBe(ServerSelectionStrategy.NoSlot);
        slot2.Should().NotBe(slot1);
    }

    [Fact]
    [Trait(TestCategories.Category, TestCategories.SimulatedConnectionFailure)]
    public async Task cluster_node_subscription_failover()
    {
        Skip.UnlessLongRunning();
        Log("Connecting...");

        await using var conn = Create(allowAdmin: true, allowSimulateConnectionFailure: true);

        var sub = conn.GetSubscriber();
        var channel = RedisChannel.Literal(Me());

        var count = 0;
        Log("Subscribing...");
        await sub.SubscribeAsync(channel, (_, val) =>
        {
            Interlocked.Increment(ref count);
            Log("Message: " + val);
        });
        sub.IsConnected(channel).Should().BeTrue();

        Log("Publishing (1)...");
        count.Should().Be(0);
        var publishedTo = await sub.PublishAsync(channel, "message1");
        // Client -> Redis -> Client -> handler takes just a moment
        await UntilConditionAsync(TimeSpan.FromSeconds(2), () => Volatile.Read(ref count) == 1);
        count.Should().Be(1);
        Log($"  Published (1) to {publishedTo} subscriber(s).");
        publishedTo.Should().Be(1);

        var endpoint = sub.SubscribedEndpoint(channel)!;
        var subscribedServer = conn.GetServer(endpoint);
        var subscribedServerEndpoint = conn.GetServerEndPoint(endpoint);

        subscribedServer.IsConnected.Should().BeTrue("subscribedServer.IsConnected");
        Assert.NotNull(subscribedServerEndpoint);
        subscribedServerEndpoint.IsConnected.Should().BeTrue("subscribedServerEndpoint.IsConnected");
        subscribedServerEndpoint.IsSubscriberConnected.Should().BeTrue("subscribedServerEndpoint.IsSubscriberConnected");

        Assert.True(conn.GetSubscriptions().TryGetValue(channel, out var subscription)); //kept as the xUnit form: it is annotated so the compiler's null-state flows to the use below
        var initialServer = subscription.GetAnyCurrentServer();
        Assert.NotNull(initialServer);
        initialServer.IsConnected.Should().BeTrue();
        Log("Connected to: " + initialServer);

        conn.AllowConnect = false;
        if (TestContext.Current.IsResp3())
        {
            subscribedServerEndpoint.SimulateConnectionFailure(SimulatedFailureType.All);

            subscribedServerEndpoint.IsConnected.Should().BeFalse("subscribedServerEndpoint.IsConnected");
            subscribedServerEndpoint.IsSubscriberConnected.Should().BeFalse("subscribedServerEndpoint.IsSubscriberConnected");
        }
        else
        {
            subscribedServerEndpoint.SimulateConnectionFailure(SimulatedFailureType.AllSubscription);

            subscribedServerEndpoint.IsConnected.Should().BeTrue("subscribedServerEndpoint.IsConnected");
            subscribedServerEndpoint.IsSubscriberConnected.Should().BeFalse("subscribedServerEndpoint.IsSubscriberConnected");
        }
        await UntilConditionAsync(TimeSpan.FromSeconds(5), () => subscription.IsConnectedAny());
        subscription.IsConnectedAny().Should().BeTrue();

        var newServer = subscription.GetAnyCurrentServer();
        Assert.NotNull(newServer);
        initialServer.Should().NotBe(newServer);
        Log("Now connected to: " + newServer);

        count = 0;
        Log("Publishing (2)...");
        count.Should().Be(0);
        publishedTo = await sub.PublishAsync(channel, "message2");
        // Client -> Redis -> Client -> handler takes just a moment
        await UntilConditionAsync(TimeSpan.FromSeconds(2), () => Volatile.Read(ref count) == 1);
        count.Should().Be(1);
        Log($"  Published (2) to {publishedTo} subscriber(s).");

        ClearAmbientFailures();
    }

    [Theory]
    [Trait(TestCategories.Category, TestCategories.SimulatedConnectionFailure)]
    [InlineData(CommandFlags.PreferMaster, true)]
    [InlineData(CommandFlags.PreferReplica, true)]
    [InlineData(CommandFlags.DemandMaster, false)]
    [InlineData(CommandFlags.DemandReplica, false)]
    public async Task primary_replica_subscription_failover(CommandFlags flags, bool expectSuccess)
    {
        Assert.Skip("TODO: Hostile");

        var config = TestConfig.Current.PrimaryServerAndPort + "," + TestConfig.Current.ReplicaServerAndPort;
        Log("Connecting...");

        await using var conn = Create(configuration: config, allowAdmin: true, allowSimulateConnectionFailure: true);

        var sub = conn.GetSubscriber();
        var channel = RedisChannel.Literal(Me() + flags.ToString()); // Individual channel per case to not overlap publishers

        var count = 0;
        Log("Subscribing...");
        await sub.SubscribeAsync(
            channel,
            (_, val) =>
            {
                Interlocked.Increment(ref count);
                Log("Message: " + val);
            },
            flags);
        sub.IsConnected(channel).Should().BeTrue();

        Log("Publishing (1)...");
        count.Should().Be(0);
        var publishedTo = await sub.PublishAsync(channel, "message1");
        // Client -> Redis -> Client -> handler takes just a moment
        await UntilConditionAsync(TimeSpan.FromSeconds(2), () => Volatile.Read(ref count) == 1);
        count.Should().Be(1);
        Log($"  Published (1) to {publishedTo} subscriber(s).");

        var endpoint = sub.SubscribedEndpoint(channel)!;
        var subscribedServer = conn.GetServer(endpoint);
        var subscribedServerEndpoint = conn.GetServerEndPoint(endpoint);

        subscribedServer.IsConnected.Should().BeTrue("subscribedServer.IsConnected");
        Assert.NotNull(subscribedServerEndpoint);
        subscribedServerEndpoint.IsConnected.Should().BeTrue("subscribedServerEndpoint.IsConnected");
        subscribedServerEndpoint.IsSubscriberConnected.Should().BeTrue("subscribedServerEndpoint.IsSubscriberConnected");

        Assert.True(conn.GetSubscriptions().TryGetValue(channel, out var subscription)); //kept as the xUnit form: it is annotated so the compiler's null-state flows to the use below
        var initialServer = subscription.GetAnyCurrentServer();
        Assert.NotNull(initialServer);
        initialServer.IsConnected.Should().BeTrue();
        Log("Connected to: " + initialServer);

        conn.AllowConnect = false;
        if (TestContext.Current.IsResp3())
        {
            subscribedServerEndpoint.SimulateConnectionFailure(SimulatedFailureType.All); // need to kill the main connection
            subscribedServerEndpoint.IsConnected.Should().BeFalse("subscribedServerEndpoint.IsConnected");
            subscribedServerEndpoint.IsSubscriberConnected.Should().BeFalse("subscribedServerEndpoint.IsSubscriberConnected");
        }
        else
        {
            subscribedServerEndpoint.SimulateConnectionFailure(SimulatedFailureType.AllSubscription);
            subscribedServerEndpoint.IsConnected.Should().BeTrue("subscribedServerEndpoint.IsConnected");
            subscribedServerEndpoint.IsSubscriberConnected.Should().BeFalse("subscribedServerEndpoint.IsSubscriberConnected");
        }

        if (expectSuccess)
        {
            await UntilConditionAsync(TimeSpan.FromSeconds(5), () => subscription.IsConnectedAny());
            subscription.IsConnectedAny().Should().BeTrue();

            var newServer = subscription.GetAnyCurrentServer();
            Assert.NotNull(newServer);
            initialServer.Should().NotBe(newServer);
            Log("Now connected to: " + newServer);
        }
        else
        {
            // This subscription shouldn't be able to reconnect by flags (demanding an unavailable server)
            await UntilConditionAsync(TimeSpan.FromSeconds(5), () => subscription.IsConnectedAny());
            subscription.IsConnectedAny().Should().BeFalse();
            Log("Unable to reconnect (as expected)");

            // Allow connecting back to the original
            conn.AllowConnect = true;
            await UntilConditionAsync(TimeSpan.FromSeconds(5), () => subscription.IsConnectedAny());
            subscription.IsConnectedAny().Should().BeTrue();

            var newServer = subscription.GetAnyCurrentServer();
            Assert.NotNull(newServer);
            initialServer.Should().Be(newServer);
            Log("Now connected to: " + newServer);
        }

        count = 0;
        Log("Publishing (2)...");
        count.Should().Be(0);
        publishedTo = await sub.PublishAsync(channel, "message2");
        // Client -> Redis -> Client -> handler takes just a moment
        await UntilConditionAsync(TimeSpan.FromSeconds(2), () => Volatile.Read(ref count) == 1);
        count.Should().Be(1);
        Log($"  Published (2) to {publishedTo} subscriber(s).");

        ClearAmbientFailures();
    }
}
