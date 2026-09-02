using System.Linq;
using System.Net;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;
using static CodeBrix.Redis.TestServer.RedisServer;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

/// <summary>
/// The id-keyed <c>CLUSTER SLOTS</c> topology is currently populated *alongside* the <c>CLUSTER NODES</c>
/// view that drives routing, so that the two can be compared before anything depends on the new one. These
/// are the comparison: they assert the shadow view agrees with what routing actually uses, and that it
/// unifies identities where the old view cannot.
/// </summary>
public class ClusterTopologyShadowUnitTests(ITestOutputHelper log)
{
    private const string Hostname = "host-1.redis.example.com";

    private static InProcessTestServer CreateServer(
        ITestOutputHelper log,
        ClusterEndpointType preferred = ClusterEndpointType.Ip,
        bool announceHostname = true)
    {
        var server = new InProcessTestServer(log) { ServerType = ServerType.Cluster, PreferredEndpointType = preferred };
        if (announceHostname) server.SetHostname(server.DefaultEndPoint, Hostname);
        return server;
    }

    /// <summary>
    /// Builds the view from an explicit <c>CLUSTER SLOTS</c> call. Autoconfigure does not ask for it yet - see
    /// the comment in <c>ServerEndPoint.AutoConfigureAsync</c> - so these exercise the model and the parser
    /// rather than the wiring; the wiring is covered where it is enabled.
    /// </summary>
    private static async Task<ClusterTopology> GetShadowAsync(IConnectionMultiplexer conn, EndPoint endpoint)
    {
        var slots = await conn.GetServer(endpoint).ClusterSlotsAsync();
        var topology = ClusterTopology.From(slots);
        //Assert.NotNull, not the fluent form: xunit annotates it [NotNull], so the compiler knows
        //`topology` is non-null on the next line. SilverAssertions carries no such postcondition.
        Assert.NotNull(topology);
        return topology;
    }

    [Theory]
    [InlineData(ClusterEndpointType.Ip)]
    [InlineData(ClusterEndpointType.Hostname)]
    public async Task shadow_topology_is_built_from_the_reply(ClusterEndpointType preferred)
    {
        //Arrange
        using var server = CreateServer(log, preferred);
        await using var conn = await server.ConnectAsync(defaultOnly: true);
        var topology = await GetShadowAsync(conn, server.DefaultEndPoint);
        var node = Assert.Single(topology.Nodes);
        log.WriteLine(node.ToString());

        //Act
        GetHost(server.DefaultEndPoint, out var port);

        //Assert
        node.Port.Should().Be(port);
        node.IsReplica.Should().BeFalse();
        string.IsNullOrEmpty(node.NodeId).Should().BeFalse();
        node.Slots.Single().From.Should().Be(0);
        node.Slots.Single().To.Should().Be(16383);
    }

    [Theory]
    [InlineData(ClusterEndpointType.Ip)]
    [InlineData(ClusterEndpointType.Hostname)]
    public async Task shadow_topology_knows_both_identities(ClusterEndpointType preferred)
    {
        // whichever form the answering node prefers, the complement arrives as metadata - so one reply is
        // enough to know the node by both names, which is what the NODES-driven view cannot express
        using var server = CreateServer(log, preferred);
        await using var conn = await server.ConnectAsync(defaultOnly: true);

        var node = Assert.Single((await GetShadowAsync(conn, server.DefaultEndPoint)).Nodes);
        var host = GetHost(server.DefaultEndPoint, out var port);

        node.Ip.Should().Be(host);
        node.Hostname.Should().Be(Hostname);
        node.Identities.Should().Contain(new IPEndPoint(IPAddress.Loopback, port));
        node.Identities.Should().Contain(new DnsEndPoint(Hostname, port));
    }

    [Fact]
    public async Task shadow_topology_agrees_with_the_routing_view()
    {
        using var server = CreateServer(log, announceHostname: false);
        GetHost(server.DefaultEndPoint, out var port);
        var other = server.AddEmptyNode(new IPEndPoint(IPAddress.Loopback, port + 1));
        server.Migrate((RedisKey)"shadow-key", other);

        await using var conn = await server.ConnectAsync();
        var api = conn.GetServer(server.DefaultEndPoint);

        // force both views to be refreshed from the same server
        var nodes = await api.ClusterNodesAsync();
        Assert.NotNull(nodes);
        var topology = await GetShadowAsync(conn, server.DefaultEndPoint);

        var fromNodes = nodes.Nodes.Where(x => !x.IsReplica && x.Slots.Count > 0)
            .Select(x => x.NodeId).OrderBy(x => x).ToArray();
        var fromShadow = topology.Nodes.Where(x => !x.IsReplica)
            .Select(x => x.NodeId).OrderBy(x => x).ToArray();

        log.WriteLine($"NODES:  {string.Join(",", fromNodes)}");
        log.WriteLine($"SHADOW: {string.Join(",", fromShadow)}");
        fromShadow.Should().Equal(fromNodes);

        // ...and the ranges agree per node, not merely in total: equal totals with different boundaries is
        // exactly what an off-by-one in range application looks like, and it is the property routing depends
        // on. Compared as sorted slot sets so that differing range *fragmentation* between the two views is
        // not treated as disagreement - only differing ownership is
        foreach (var node in topology.Nodes.Where(x => !x.IsReplica))
        {
            var expected = Slots(nodes.Nodes.Single(x => x.NodeId == node.NodeId).Slots);
            var actual = Slots(node.Slots);
            log.WriteLine($"{node.NodeId}: {actual.Length} slots");
            actual.Should().Equal(expected);
        }

        static int[] Slots(System.Collections.Generic.IEnumerable<SlotRange> ranges)
            => ranges.SelectMany(r => Enumerable.Range(r.From, r.To - r.From + 1)).OrderBy(x => x).ToArray();
    }

    [Fact]
    public async Task shadow_topology_does_not_change_routing()
    {
        // the whole point of shadow mode: recorded, compared, not acted upon
        using var server = CreateServer(log, ClusterEndpointType.Hostname);
        await using var conn = await server.ConnectAsync(defaultOnly: true);

        Assert.NotNull((await GetShadowAsync(conn, server.DefaultEndPoint)));
        // endpoints are still exactly what NODES gave us: addresses, not the preferred hostname form
        conn.GetEndPoints().Should().AllSatisfy(ep => Assert.IsType<IPEndPoint>(ep));
        await conn.GetDatabase().StringSetAsync("shadow-routing", "ok");
        (await conn.GetDatabase().StringGetAsync("shadow-routing")).Should().Be("ok");
    }

    [Fact]
    public async Task pre_four_zero_server_yields_no_shadow_topology()
    {
        // no node ids to key on, so there is nothing we could reconcile; better absent than half-built
        using var server = new InProcessTestServer(log)
        {
            ServerType = ServerType.Cluster,
            RedisVersion = new System.Version(3, 2, 0),
        };
        await using var conn = await server.ConnectAsync(defaultOnly: true);

        var slots = await conn.GetServer(server.DefaultEndPoint).ClusterSlotsAsync();
        log.WriteLine($"topology: {ClusterTopology.From(slots)?.Nodes.Count.ToString() ?? "(none)"}");
    }
}
