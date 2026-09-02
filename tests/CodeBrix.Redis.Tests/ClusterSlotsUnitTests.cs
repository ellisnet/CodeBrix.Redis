using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;
using static CodeBrix.Redis.TestServer.RedisServer;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

/// <summary>
/// The parsed <c>CLUSTER SLOTS</c> view, exercised against the toy server across the naming configurations a
/// real server can be in - including the placeholder endpoint values, which are the classic client bug.
/// </summary>
public class ClusterSlotsUnitTests(ITestOutputHelper log)
{
    private const string Hostname = "host-1.redis.example.com";
    private static readonly System.Version BeforeHostnames = new(6, 2, 0);

    private static InProcessTestServer CreateServer(
        ITestOutputHelper log,
        ClusterEndpointType preferred = ClusterEndpointType.Ip,
        bool announceHostname = true,
        AnnouncedAddress announced = AnnouncedAddress.Address,
        System.Version? version = null)
    {
        var server = new InProcessTestServer(log) { ServerType = ServerType.Cluster, PreferredEndpointType = preferred };
        if (version is not null) server.RedisVersion = version;
        if (announceHostname) server.SetHostname(server.DefaultEndPoint, Hostname);
        if (announced != AnnouncedAddress.Address) server.SetAnnouncedAddress(server.DefaultEndPoint, announced);
        return server;
    }

    private static async Task<ClusterSlotNode> GetPrimaryAsync(InProcessTestServer server)
    {
        await using var conn = await server.ConnectAsync(defaultOnly: true);
        var result = await conn.GetServer(server.DefaultEndPoint).ClusterSlotsAsync();
        Assert.NotNull(result);
        return Assert.Single(result.Assignments).Primary;
    }

    [Fact]
    public void node_ids_are_unique_even_when_created_in_the_same_tick()
    {
        // regression: the toy server created a Random per id, and .NET Framework seeds that from the tick
        // count - so nodes created in the same tick shared an id. Keyed reconciliation then merged two nodes
        // into one, which surfaced as the *client* looking wrong
        using var server = new InProcessTestServer(log) { ServerType = ServerType.Cluster };
        var ids = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);

        GetHost(server.DefaultEndPoint, out var port);
        server.TryGetNode(server.DefaultEndPoint, out var first).Should().BeTrue();
        ids.Add(first.Id).Should().BeTrue();

        for (int i = 1; i <= 25; i++)
        {
            var endpoint = server.AddEmptyNode(new IPEndPoint(IPAddress.Loopback, port + i));
            server.TryGetNode(endpoint, out var node).Should().BeTrue();
            ids.Add(node.Id).Should().BeTrue($"duplicate id at node {i}: {node.Id}");
        }
    }

    [Fact]
    public async Task whole_keyspace_is_reported_with_node_id_and_endpoint()
    {
        using var server = CreateServer(log, announceHostname: false);
        var host = GetHost(server.DefaultEndPoint, out var port);

        await using var conn = await server.ConnectAsync(defaultOnly: true);
        var result = await conn.GetServer(server.DefaultEndPoint).ClusterSlotsAsync();
        Assert.NotNull(result);
        var assignment = Assert.Single(result.Assignments);
        assignment.Slots.From.Should().Be(0);
        assignment.Slots.To.Should().Be(16383);
        assignment.Replicas.Should().BeEmpty();

        var primary = assignment.Primary;
        primary.AnnouncedEndpoint.Should().Be(host);
        primary.Port.Should().Be(port);
        primary.EndPoint.Should().Be(new IPEndPoint(IPAddress.Loopback, port));
        string.IsNullOrEmpty(primary.NodeId).Should().BeFalse();
        primary.Metadata.Should().BeEmpty();
    }

    [Fact]
    public async Task ip_preferred_surfaces_hostname_from_metadata()
    {
        using var server = CreateServer(log, ClusterEndpointType.Ip);
        var host = GetHost(server.DefaultEndPoint, out var port);

        var primary = await GetPrimaryAsync(server);

        primary.AnnouncedEndpoint.Should().Be(host);
        primary.EndPoint.Should().Be(new IPEndPoint(IPAddress.Loopback, port));
        primary.Hostname.Should().Be(Hostname);
        primary.Ip.Should().BeNull(); // the complement rule: it is already the primary field
    }

    [Fact]
    public async Task hostname_preferred_parses_as_a_dns_end_point_and_surfaces_ip()
    {
        //Arrange
        using var server = CreateServer(log, ClusterEndpointType.Hostname);
        var host = GetHost(server.DefaultEndPoint, out var port);

        //Act
        var primary = await GetPrimaryAsync(server);

        //Assert
        primary.AnnouncedEndpoint.Should().Be(Hostname);
        primary.EndPoint.Should().Be(new DnsEndPoint(Hostname, port));
        primary.Ip.Should().Be(host);
        primary.Hostname.Should().BeNull();
    }

    [Fact]
    public async Task unannounced_hostname_yields_no_endpoint_but_keeps_the_ip()
    {
        using var server = CreateServer(log, ClusterEndpointType.Hostname, announceHostname: false);
        var host = GetHost(server.DefaultEndPoint, out _);

        var primary = await GetPrimaryAsync(server);

        // "?" is an explicitly unknown node, so it must not become an endpoint...
        primary.AnnouncedEndpoint.Should().Be("?");
        primary.EndPoint.Should().BeNull();

        // ...but the reply still carries the address, so the union is not poorer than CLUSTER NODES
        primary.Ip.Should().Be(host);
    }

    [Fact]
    public async Task null_endpoint_yields_no_endpoint()
    {
        using var server = CreateServer(log, ClusterEndpointType.UnknownEndpoint);
        var host = GetHost(server.DefaultEndPoint, out var port);

        var primary = await GetPrimaryAsync(server);

        // null means "connect to where you sent this, with this port" - a caller decision, not ours
        primary.AnnouncedEndpoint.Should().BeNull();
        primary.EndPoint.Should().BeNull();
        primary.Port.Should().Be(port);
        primary.Ip.Should().Be(host);
        primary.Hostname.Should().Be(Hostname);
    }

    [Fact]
    public async Task empty_endpoint_yields_no_endpoint()
    {
        //Arrange
        using var server = CreateServer(log, ClusterEndpointType.Ip, announced: AnnouncedAddress.Empty);

        //Act
        var primary = await GetPrimaryAsync(server);

        //Assert
        primary.AnnouncedEndpoint.Should().Be("");
        primary.EndPoint.Should().BeNull();
    }

    [Fact]
    public async Task recognized_keys_are_surfaced_and_unknown_ones_preserved()
    {
        // known keys are matched over the raw bytes and surfaced as properties, so they cost no allocation
        // and do not appear in Metadata; the extensible remainder is kept as declared
        using var server = CreateServer(log, ClusterEndpointType.Hostname);
        server.SetSlotsMetadata(server.DefaultEndPoint, "not-invented-yet", "42");
        var host = GetHost(server.DefaultEndPoint, out _);

        var primary = await GetPrimaryAsync(server);

        primary.Ip.Should().Be(host); // recognized...
        Assert.Single(primary.Metadata).Should().Be(new KeyValuePair<string, string?>("not-invented-yet", "42")); // ...and the rest kept
    }

    [Fact]
    public async Task metadata_keys_are_matched_without_regard_to_case()
    {
        // the contract renders these keys inconsistently between prose and examples, so casing cannot be
        // relied on; an upper-case key must still be recognized rather than landing in Metadata
        using var server = CreateServer(log, ClusterEndpointType.Ip, announceHostname: false);
        server.SetSlotsMetadata(server.DefaultEndPoint, "HOSTNAME", "shouty.redis.example.com");

        var primary = await GetPrimaryAsync(server);

        primary.Hostname.Should().Be("shouty.redis.example.com");
        primary.Metadata.Should().BeEmpty();
    }

    [Fact]
    public async Task pre_seven_zero_server_reports_no_metadata()
    {
        using var server = CreateServer(log, ClusterEndpointType.Hostname, version: BeforeHostnames);
        var host = GetHost(server.DefaultEndPoint, out var port);

        var primary = await GetPrimaryAsync(server);

        // three-element node block: endpoint, port, id - and the preference is inert below 7.0
        primary.AnnouncedEndpoint.Should().Be(host);
        primary.EndPoint.Should().Be(new IPEndPoint(IPAddress.Loopback, port));
        primary.Metadata.Should().BeEmpty();
        primary.Hostname.Should().BeNull();
        string.IsNullOrEmpty(primary.NodeId).Should().BeFalse();
    }

    [Fact]
    public async Task migrated_slots_are_reported_as_separate_assignments()
    {
        using var server = CreateServer(log, announceHostname: false);
        GetHost(server.DefaultEndPoint, out var port);
        var other = server.AddEmptyNode(new IPEndPoint(IPAddress.Loopback, port + 1));
        server.Migrate((RedisKey)"slots-key", other);

        await using var conn = await server.ConnectAsync();
        var result = await conn.GetServer(server.DefaultEndPoint).ClusterSlotsAsync();
        Assert.NotNull(result);
        log.WriteLine(string.Join(", ", result.Assignments.Select(x => $"{x.Slots}=>{x.Primary}")));

        // the migrated slot splits the original range, and every assignment names its own primary
        (result.Assignments.Count > 1).Should().BeTrue();
        result.Assignments.Should().AllSatisfy(x => x.Primary.Should().NotBeNull());
        result.Assignments.Should().Contain(x => x.Primary.Port == port + 1);

        var migrated = Assert.Single(result.Assignments, x => x.Primary.Port == port + 1);
        migrated.Slots.To.Should().Be(migrated.Slots.From); // exactly the one slot moved
    }

    [Fact]
    public async Task node_id_is_stable_across_the_naming_forms()
    {
        // node-id is the one identity that does not depend on how the answering node renders endpoints,
        // which is what makes it the reliable reconciliation key
        using var server = CreateServer(log, ClusterEndpointType.Ip);
        var byAddress = await GetPrimaryAsync(server);

        server.PreferredEndpointType = ClusterEndpointType.Hostname;
        var byName = await GetPrimaryAsync(server);

        byName.AnnouncedEndpoint.Should().NotBe(byAddress.AnnouncedEndpoint);
        byName.NodeId.Should().Be(byAddress.NodeId);
    }
}
