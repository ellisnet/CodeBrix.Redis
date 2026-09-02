using System.Linq;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class Roles(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    protected override string GetConfiguration() => TestConfig.Current.PrimaryServerAndPort + "," + TestConfig.Current.ReplicaServerAndPort;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task primary_role(bool allowAdmin) // should work with or without admin now
    {
        SkipOnWindowsRelease();
        await using var conn = Create(allowAdmin: allowAdmin);
        var servers = conn.GetServers();
        Log("Server list:");
        foreach (var s in servers)
        {
            Log($"  Server: {s.EndPoint} (isConnected: {s.IsConnected}, isReplica: {s.IsReplica})");
        }
        var server = servers.First(conn => !conn.IsReplica);
        var role = server.Role();
        Log($"Chosen primary: {server.EndPoint} (role: {role})");
        if (allowAdmin)
        {
            Log($"Info (Replication) dump for {server.EndPoint}:");
            Log(server.InfoRaw("Replication"));
            Log("");

            foreach (var s in servers)
            {
                if (s.IsReplica)
                {
                    Log($"Info (Replication) dump for {s.EndPoint}:");
                    Log(s.InfoRaw("Replication"));
                    Log("");
                }
            }
        }
        Assert.NotNull(role);
        role.Value.Should().Be(Role.LabelForMaster);
        var primary = role as Role.Master;
        Assert.NotNull(primary);
        Assert.NotNull(primary.Replicas);
        // Only do this check for Redis > 4 (to exclude Redis 3.x on Windows).
        // Unrelated to this test, the replica isn't connecting and we'll revisit swapping the server out.
        // TODO: MemuraiDeveloper check
        if (server.Version > RedisFeatures.v4_0_0)
        {
            Log($"Searching for: {TestConfig.Current.ReplicaServer}:{TestConfig.Current.ReplicaPort}");
            Log($"Replica count: {primary.Replicas.Count}");

            primary.Replicas.Should().NotBeEmpty();
            foreach (var replica in primary.Replicas)
            {
                Log($"  Replica: {replica.Ip}:{replica.Port} (offset: {replica.ReplicationOffset})");
                Log(replica.ToString());
            }
            primary.Replicas.Should().Contain(r => r.Ip == TestConfig.Current.ReplicaServer && r.Port == TestConfig.Current.ReplicaPort);
        }
    }

    [Fact]
    public async Task replica_role()
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        await using var conn = await ConnectionMultiplexer.ConnectAsync($"{TestConfig.Current.ReplicaServerAndPort},allowAdmin=true");
        var server = conn.GetServers().First(conn => conn.IsReplica);

        var role = server.Role();
        Assert.NotNull(role);
        var replica = role as Role.Replica;
        Assert.NotNull(replica);
        TestConfig.Current.PrimaryServer.Should().Be(replica.MasterIp);
        TestConfig.Current.PrimaryPort.Should().Be(replica.MasterPort);
    }
}
