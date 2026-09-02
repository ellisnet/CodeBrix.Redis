using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public abstract class GetServerTestsBase(ITestOutputHelper output, SharedConnectionFixture fixture)
    : TestBase(output, fixture)
{
    protected abstract bool IsCluster { get; }

    [Fact]
    public async Task get_servers_memoization()
    {
        await using var conn = Create();

        var servers0 = conn.GetServers();
        var servers1 = conn.GetServers();

        // different array, exact same contents
        servers1.Should().NotBeSameAs(servers0);
        servers0.Should().NotBeEmpty();
        servers0.Should().NotBeNull();
        servers1.Should().NotBeNull();
        servers1.Length.Should().Be(servers0.Length);
        for (int i = 0; i < servers0.Length; i++)
        {
            servers1[i].Should().BeSameAs(servers0[i]);
        }
    }

    [Fact]
    public async Task get_server_by_endpoint_memoization()
    {
        await using var conn = Create();
        var ep = conn.GetEndPoints().First();

        IServer x = conn.GetServer(ep), y = conn.GetServer(ep);
        y.Should().BeSameAs(x);

        object asyncState = "whatever";
        x = conn.GetServer(ep, asyncState);
        y = conn.GetServer(ep, asyncState);
        y.Should().NotBeSameAs(x);
    }

    [Fact]
    public async Task get_server_by_key_memoization()
    {
        await using var conn = Create();
        RedisKey key = Me();
        string value = $"{key}:value";
        await conn.GetDatabase().StringSetAsync(key, value);

        IServer x = conn.GetServer(key), y = conn.GetServer(key);
        y.IsReplica.Should().BeFalse("IsReplica");
        y.Should().BeSameAs(x);

        y = conn.GetServer(key, flags: CommandFlags.DemandMaster);
        y.Should().BeSameAs(x);

        // async state demands separate instance
        y = conn.GetServer(key, "async state", flags: CommandFlags.DemandMaster);
        y.Should().NotBeSameAs(x);

        // primary and replica should be different
        y = conn.GetServer(key, flags: CommandFlags.DemandReplica);
        y.Should().NotBeSameAs(x);
        y.IsReplica.Should().BeTrue("IsReplica");

        // replica again: same
        var z = conn.GetServer(key, flags: CommandFlags.DemandReplica);
        z.Should().BeSameAs(y);

        // check routed correctly
        var actual = (string?)await x.ExecuteAsync(null, "get", [key], CommandFlags.NoRedirect);
        actual.Should().Be(value); // check value against primary

        // for replica, don't check the value, because of replication delay - just: no error
        _ = y.ExecuteAsync(null, "get", [key], CommandFlags.NoRedirect);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task get_server_with_default_key(bool explicitNull)
    {
        await using var conn = Create();
        bool isCluster = conn.ServerSelectionStrategy.ServerType == ServerType.Cluster;
        isCluster.Should().Be(IsCluster); // check our assumptions!

        // we expect explicit null and default to act the same, but: check
        RedisKey key = explicitNull ? RedisKey.Null : default(RedisKey);

        IServer primary = conn.GetServer(key);
        primary.IsReplica.Should().BeFalse();

        IServer replica = conn.GetServer(key, flags: CommandFlags.DemandReplica);
        replica.IsReplica.Should().BeTrue();

        // check multiple calls
        HashSet<IServer> uniques = [];
        for (int i = 0; i < 100; i++)
        {
            uniques.Add(conn.GetServer(key));
        }

        if (isCluster)
        {
            (uniques.Count > 1).Should().BeTrue(); // should be able to get arbitrary servers
        }
        else
        {
            uniques.Should().ContainSingle();
        }

        uniques.Clear();
        for (int i = 0; i < 100; i++)
        {
            uniques.Add(conn.GetServer(key, flags: CommandFlags.DemandReplica));
        }

        if (isCluster)
        {
            (uniques.Count > 1).Should().BeTrue(); // should be able to get arbitrary servers
        }
        else
        {
            uniques.Should().ContainSingle();
        }
    }
}

[RunPerProtocol]
public class GetServerTestsCluster(ITestOutputHelper output, SharedConnectionFixture fixture) : GetServerTestsBase(output, fixture)
{
    protected override string GetConfiguration() => GetClusterConfiguration(string.Empty);

    protected override bool IsCluster => true;
}

[RunPerProtocol]
public class GetServerTestsStandalone(ITestOutputHelper output, SharedConnectionFixture fixture) : GetServerTestsBase(output, fixture)
{
    protected override string GetConfiguration() => // we want to test flags usage including replicas
        TestConfig.Current.PrimaryServerAndPort + "," + TestConfig.Current.ReplicaServerAndPort;

    protected override bool IsCluster => false;
}
