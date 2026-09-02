using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Redis.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class DefaultOptionsTests(ITestOutputHelper output) : TestBase(output)
{
    public class TestOptionsProvider(string domainSuffix) : DefaultOptionsProvider
    {
        private readonly string _domainSuffix = domainSuffix;

        public override bool AbortOnConnectFail => true;
        public override TimeSpan? ConnectTimeout => TimeSpan.FromSeconds(123);
        public override bool AllowAdmin => true;
        public override BacklogPolicy BacklogPolicy => BacklogPolicy.FailFast;
        public override bool CheckCertificateRevocation => true;
        public override CommandMap CommandMap => CommandMap.Create(new HashSet<string>() { "SELECT" });
        public override TimeSpan ConfigCheckInterval => TimeSpan.FromSeconds(124);
        public override string ConfigurationChannel => "TestConfigChannel";
        public override int ConnectRetry => 123;
        public override Version DefaultVersion => new Version(1, 2, 3, 4);
        protected override string GetDefaultClientName() => "TestPrefix-" + base.GetDefaultClientName();
        public override bool HeartbeatConsistencyChecks => true;
        public override TimeSpan HeartbeatInterval => TimeSpan.FromMilliseconds(500);
        public override bool IsMatch(EndPoint endpoint) => endpoint is DnsEndPoint dnsep && dnsep.Host.EndsWith(_domainSuffix);
        public override TimeSpan KeepAliveInterval => TimeSpan.FromSeconds(125);
        public override ILoggerFactory? LoggerFactory => NullLoggerFactory.Instance;
        public override Proxy Proxy => Proxy.Twemproxy;
        public override IReconnectRetryPolicy ReconnectRetryPolicy => new TestRetryPolicy();
        public override bool ResolveDns => true;
        public override TimeSpan SyncTimeout => TimeSpan.FromSeconds(126);
        public override string TieBreaker => "TestTiebreaker";
        public override string? User => "TestUser";
        public override string? Password => "TestPassword";
    }

    public class TestRetryPolicy : IReconnectRetryPolicy
    {
        public bool ShouldRetry(long currentRetryCount, int timeElapsedMillisecondsSinceLastRetry) => false;
    }

    [Fact]
    public void is_match_on_domain()
    {
        DefaultOptionsProvider.AddProvider(new TestOptionsProvider(".testdomain"));

        var epc = new EndPointCollection(new List<EndPoint>() { new DnsEndPoint("local.testdomain", 0) });
        var provider = DefaultOptionsProvider.GetProvider(epc);
        provider.Should().BeOfType<TestOptionsProvider>();

        epc = new EndPointCollection(new List<EndPoint>() { new DnsEndPoint("local.nottestdomain", 0) });
        provider = DefaultOptionsProvider.GetProvider(epc);
        provider.Should().BeOfType<DefaultOptionsProvider>();
    }

    [Theory]
    [InlineData("contoso.redis.cache.windows.net")]
    [InlineData("contoso.REDIS.CACHE.chinacloudapi.cn")] // added a few upper case chars to validate comparison
    [InlineData("contoso.redis.cache.usgovcloudapi.net")]
    [InlineData("contoso.redis.cache.sovcloud-api.de")]
    [InlineData("contoso.redis.cache.sovcloud-api.fr")]
    public void is_match_on_azure_domain(string hostName)
    {
        var epc = new EndPointCollection(new List<EndPoint>() { new DnsEndPoint(hostName, 0) });
        var provider = DefaultOptionsProvider.GetProvider(epc);
        provider.Should().BeOfType<AzureOptionsProvider>();
    }

    [Theory]
    [InlineData("contoso.redis.azure.net")]
    [InlineData("contoso.redis.chinacloudapi.cn")]
    [InlineData("contoso.redis.usgovcloudapi.net")]
    [InlineData("contoso.redisenterprise.cache.azure.net")]
    public void is_match_on_azure_managed_redis_domain(string hostName)
    {
        var epc = new EndPointCollection(new List<EndPoint>() { new DnsEndPoint(hostName, 0) });
        var provider = DefaultOptionsProvider.GetProvider(epc);
        provider.Should().BeOfType<AzureManagedRedisOptionsProvider>();
    }

    [Theory]
    [InlineData(RedisProtocol.Resp2)]
    [InlineData(RedisProtocol.Resp3)]
    public async Task azure_managed_redis_connects_without_subscription_connection(RedisProtocol protocol)
    {
        using var serverObj = new InProcessTestServer(Output, new DnsEndPoint("contoso.redis.azure.net", 10000), useSsl: true);
        var config = serverObj.GetClientConfig();
        config.ClientName = Guid.NewGuid().ToString().Replace("-", "");
        config.Protocol = protocol;

        await using var conn = await ConnectionMultiplexer.ConnectAsync(config, Writer);

        var server = conn.GetServer(conn.GetEndPoints().Single());
        var interactiveId = ((IInternalConnectionMultiplexer)conn).GetConnectionId(server.EndPoint, ConnectionType.Interactive);
        var clients = await server.ClientListAsync();
        var namedClients = clients.Where(x => x.Name == config.ClientName).ToArray();

        server.Protocol.Should().Be(protocol);
        interactiveId.Should().NotBeNull();
        var self = Assert.Single(clients, x => x.Id == interactiveId);
        self.ClientType.Should().Be(ClientType.Normal);
        self.SubscriptionCount.Should().Be(0);
        self.PatternSubscriptionCount.Should().Be(0);
        self.ShardedSubscriptionCount.Should().Be(0);
        self.Protocol.Should().Be(protocol);

        var expectedCount = protocol is RedisProtocol.Resp3 ? 1 : 2;
        serverObj.ClientCount.Should().Be(expectedCount);
        namedClients.Length.Should().Be(expectedCount);

        await AssertCanPubSubAsync(conn, $"{nameof(azure_managed_redis_connects_without_subscription_connection)}:{protocol}");
    }

    [Fact]
    public async Task vanilla_resp2_connects_with_separate_pub_sub_connection()
    {
        using var serverObj = new InProcessTestServer(Output, new DnsEndPoint("redis.contoso.com", 10000), useSsl: true);
        var config = serverObj.GetClientConfig();
        config.Protocol = RedisProtocol.Resp2;
        Log($"QueueWhileDisconnected: {config.BacklogPolicy.QueueWhileDisconnected}");

        await using var conn = await ConnectionMultiplexer.ConnectAsync(config, Writer);
        var sub = conn.GetSubscriber();
        await sub.SubscribeAsync(RedisChannel.Literal(nameof(vanilla_resp2_connects_with_separate_pub_sub_connection)), (_, _) => { });

        var server = conn.GetServer(conn.GetEndPoints().Single());
        var mux = (IInternalConnectionMultiplexer)conn;
        var interactiveId = mux.GetConnectionId(server.EndPoint, ConnectionType.Interactive);
        var subscriptionId = mux.GetConnectionId(server.EndPoint, ConnectionType.Subscription);
        var clients = server.ClientList();
        var namedClients = clients.Where(x => x.Name == conn.ClientName).ToArray();

        server.Protocol.Should().Be(RedisProtocol.Resp2);
        serverObj.ClientCount.Should().Be(2);
        interactiveId.Should().NotBeNull();
        subscriptionId.Should().NotBeNull();
        subscriptionId.Should().NotBe(interactiveId);
        namedClients.Length.Should().Be(2);

        var interactive = Assert.Single(clients, x => x.Id == interactiveId);
        var subscription = Assert.Single(clients, x => x.Id == subscriptionId);
        interactive.ClientType.Should().Be(ClientType.Normal);
        subscription.ClientType.Should().Be(ClientType.PubSub);
        (subscription.SubscriptionCount > 0).Should().BeTrue();

        await AssertCanPubSubAsync(conn, nameof(vanilla_resp2_connects_with_separate_pub_sub_connection));
    }

    private static async Task AssertCanPubSubAsync(ConnectionMultiplexer conn, string channelName)
    {
        var sub = conn.GetSubscriber();
        var channel = RedisChannel.Literal(channelName);
        var payload = (RedisValue)("payload:" + channelName);
        TaskCompletionSource<RedisValue> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        await sub.SubscribeAsync(channel, (_, message) => tcs.TrySetResult(message));
        try
        {
            await sub.PublishAsync(channel, payload);
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(5000, TestContext.Current.CancellationToken));
            completed.Should().BeSameAs(tcs.Task);
            (await tcs.Task).Should().Be(payload);
        }
        finally
        {
            await sub.UnsubscribeAsync(channel);
        }
    }

    [Fact]
    public void all_overrides_from_defaults_prop()
    {
        var options = ConfigurationOptions.Parse("localhost");
        options.Defaults.Should().BeOfType<DefaultOptionsProvider>();
        options.Defaults = new TestOptionsProvider("");
        options.Defaults.Should().BeOfType<TestOptionsProvider>();
        AssertAllOverrides(options);
    }

    [Fact]
    public void all_overrides_from_endpoints_parse()
    {
        DefaultOptionsProvider.AddProvider(new TestOptionsProvider(".parse"));
        var options = ConfigurationOptions.Parse("localhost.parse:6379");
        options.Defaults.Should().BeOfType<TestOptionsProvider>();
        AssertAllOverrides(options);
    }

    private static void AssertAllOverrides(ConfigurationOptions options)
    {
        options.AbortOnConnectFail.Should().BeTrue();
        TimeSpan.FromMilliseconds(options.ConnectTimeout).Should().Be(TimeSpan.FromSeconds(123));

        options.AllowAdmin.Should().BeTrue();
        options.BacklogPolicy.Should().Be(BacklogPolicy.FailFast);
        options.CheckCertificateRevocation.Should().BeTrue();

        options.CommandMap.IsAvailable(RedisCommand.SELECT).Should().BeTrue();
        options.CommandMap.IsAvailable(RedisCommand.GET).Should().BeFalse();

        TimeSpan.FromSeconds(options.ConfigCheckSeconds).Should().Be(TimeSpan.FromSeconds(124));
        options.ConfigurationChannel.Should().Be("TestConfigChannel");
        options.ConnectRetry.Should().Be(123);
        options.DefaultVersion.Should().Be(new Version(1, 2, 3, 4));

        options.HeartbeatConsistencyChecks.Should().BeTrue();
        options.HeartbeatInterval.Should().Be(TimeSpan.FromMilliseconds(500));

        TimeSpan.FromSeconds(options.KeepAlive).Should().Be(TimeSpan.FromSeconds(125));
        options.LoggerFactory.Should().Be(NullLoggerFactory.Instance);
        options.Proxy.Should().Be(Proxy.Twemproxy);
        options.ReconnectRetryPolicy.Should().BeOfType<TestRetryPolicy>();
        options.ResolveDns.Should().BeTrue();
        TimeSpan.FromMilliseconds(options.SyncTimeout).Should().Be(TimeSpan.FromSeconds(126));
        options.TieBreaker.Should().Be("TestTiebreaker");
        options.User.Should().Be("TestUser");
        options.Password.Should().Be("TestPassword");
    }

    public class TestAfterConnectOptionsProvider : DefaultOptionsProvider
    {
        public int Calls;

        public override Task AfterConnectAsync(ConnectionMultiplexer muxer, Action<string> log)
        {
            Interlocked.Increment(ref Calls);
            log("TestAfterConnectOptionsProvider.AfterConnectAsync!");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task after_connect_async_handler()
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        //Arrange
        var options = ConfigurationOptions.Parse(GetConfiguration());
        var provider = new TestAfterConnectOptionsProvider();
        options.Defaults = provider;

        //Act
        await using var conn = await ConnectionMultiplexer.ConnectAsync(options, Writer);

        //Assert
        conn.IsConnected.Should().BeTrue();
        provider.Calls.Should().Be(1);
    }

    public class TestClientNameOptionsProvider : DefaultOptionsProvider
    {
        protected override string GetDefaultClientName() => "Hey there";
    }

    [Fact]
    public async Task client_name_override()
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        //Arrange
        var options = ConfigurationOptions.Parse(GetConfiguration());
        options.Defaults = new TestClientNameOptionsProvider();

        //Act
        await using var conn = await ConnectionMultiplexer.ConnectAsync(options, Writer);

        //Assert
        conn.IsConnected.Should().BeTrue();
        conn.ClientName.Should().Be("Hey there");
    }

    [Fact]
    public async Task client_name_explicit_wins()
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        //Arrange
        var options = ConfigurationOptions.Parse(GetConfiguration() + ",name=FooBar");
        options.Defaults = new TestClientNameOptionsProvider();

        //Act
        await using var conn = await ConnectionMultiplexer.ConnectAsync(options, Writer);

        //Assert
        conn.IsConnected.Should().BeTrue();
        conn.ClientName.Should().Be("FooBar");
    }

    public class TestLibraryNameOptionsProvider : DefaultOptionsProvider
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public override string LibraryName => Id;
    }

    [Fact]
    public async Task library_name_override()
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        var options = ConfigurationOptions.Parse(GetConfiguration());
        var defaults = new TestLibraryNameOptionsProvider();
        options.AllowAdmin = true;
        options.Defaults = defaults;

        await using var conn = await ConnectionMultiplexer.ConnectAsync(options, Writer);
        // CLIENT SETINFO is in 7.2.0+
        TestBase.ThrowIfBelowMinVersion(conn, RedisFeatures.v7_2_0_rc1);

        var clients = await GetServer(conn).ClientListAsync();
        foreach (var client in clients)
        {
            Log("Library name: " + client.LibraryName);
        }

        conn.IsConnected.Should().BeTrue();
        clients.Any(c => c.LibraryName == defaults.LibraryName).Should().BeTrue("Did not find client with name: " + defaults.Id);
    }
}
