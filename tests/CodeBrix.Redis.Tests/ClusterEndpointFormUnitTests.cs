using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;
using static CodeBrix.Redis.TestServer.RedisServer;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

/// <summary>
/// The toy server's <c>CLUSTER SLOTS</c> and <c>-MOVED</c> naming, per its preferred endpoint type.
/// Nothing consumes this client-side yet; it exists so that identity handling has something faithful
/// to be written against, including the placeholder endpoint values the contract prescribes.
/// </summary>
public class ClusterEndpointFormUnitTests(ITestOutputHelper log)
{
    private const string Hostname = "host-1.redis.example.com";

    // hostnames and the endpoint-type preference are 7.0 features, which the in-process server now
    // declares by default; the pre-7.0 shape is what has to be asked for
    private static readonly System.Version BeforeHostnames = new(6, 2, 0);

    private static InProcessTestServer CreateServer(
        ITestOutputHelper log,
        ClusterEndpointType preferred,
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

    /// <summary>Returns the [endpoint, port, id, metadata] node block of the first slot range.</summary>
    private static async Task<RedisResult[]> GetFirstNodeAsync(InProcessTestServer server)
    {
        await using var conn = await server.ConnectAsync(defaultOnly: true);
        var slots = await conn.GetServer(server.DefaultEndPoint).ExecuteAsync("cluster", "slots");

        // each range is [from, to, master, replica...]; we want the master block of the first range
        var ranges = (RedisResult[])slots!;
        var firstRange = (RedisResult[])ranges[0]!;
        return (RedisResult[])firstRange[2]!;
    }

    /// <summary>
    /// Returns the master node block of every slot range, keyed by port, as described by
    /// <paramref name="askOf"/> - which node answers matters, since the preference is theirs.
    /// </summary>
    private static async Task<Dictionary<int, RedisResult[]>> GetNodesByPortAsync(InProcessTestServer server, EndPoint? askOf = null)
    {
        await using var conn = await server.ConnectAsync();
        var slots = await conn.GetServer(askOf ?? server.DefaultEndPoint).ExecuteAsync("cluster", "slots");

        var result = new Dictionary<int, RedisResult[]>();
        foreach (var rangeResult in (RedisResult[])slots!)
        {
            var range = (RedisResult[])rangeResult!;
            var node = (RedisResult[])range[2]!;
            result[(int)node[1]] = node;
        }
        return result;
    }

    private static string? GetMetadata(RedisResult[] node, string key)
    {
        var metadata = (RedisResult[])node[3]!;
        for (int i = 0; i + 1 < metadata.Length; i += 2)
        {
            // the contract renders these keys inconsistently, so match without regard to case
            if (string.Equals((string?)metadata[i], key, System.StringComparison.OrdinalIgnoreCase))
            {
                return (string?)metadata[i + 1];
            }
        }
        return null;
    }

    [Fact]
    public async Task ip_preferred_puts_address_first_and_hostname_in_metadata()
    {
        using var server = CreateServer(log, ClusterEndpointType.Ip);
        var host = GetHost(server.DefaultEndPoint, out _);

        var node = await GetFirstNodeAsync(server);

        ((string?)node[0]).Should().Be(host);
        GetMetadata(node, "hostname").Should().Be(Hostname);
        GetMetadata(node, "ip").Should().BeNull(); // the complement rule: no ip, it is already the primary
    }

    [Fact]
    public async Task hostname_preferred_puts_hostname_first_and_address_in_metadata()
    {
        //Arrange
        using var server = CreateServer(log, ClusterEndpointType.Hostname);
        var host = GetHost(server.DefaultEndPoint, out _);

        //Act
        var node = await GetFirstNodeAsync(server);

        //Assert
        ((string?)node[0]).Should().Be(Hostname);
        GetMetadata(node, "ip").Should().Be(host);
        GetMetadata(node, "hostname").Should().BeNull();
    }

    [Fact]
    public async Task hostname_preferred_but_unannounced_reports_question_mark()
    {
        using var server = CreateServer(log, ClusterEndpointType.Hostname, announceHostname: false);

        var node = await GetFirstNodeAsync(server);

        // "?" means an unknown node - explicitly *not* "the node serving this command"
        ((string?)node[0]).Should().Be("?");
    }

    [Fact]
    public async Task unknown_endpoint_preferred_reports_null()
    {
        using var server = CreateServer(log, ClusterEndpointType.UnknownEndpoint);
        var host = GetHost(server.DefaultEndPoint, out _);

        var node = await GetFirstNodeAsync(server);

        node[0].IsNull.Should().BeTrue();

        // both forms move into the metadata, since neither is the primary
        GetMetadata(node, "ip").Should().Be(host);
        GetMetadata(node, "hostname").Should().Be(Hostname);
    }

    [Fact]
    public async Task node_that_does_not_know_its_address_reports_empty()
    {
        //Arrange
        using var server = CreateServer(log, ClusterEndpointType.Ip, announced: AnnouncedAddress.Empty);

        //Act
        var node = await GetFirstNodeAsync(server);

        //Assert
        ((string?)node[0]).Should().Be("");
    }

    [Fact]
    public async Task node_with_no_address_known_to_the_server_reports_null()
    {
        //Arrange
        using var server = CreateServer(log, ClusterEndpointType.Ip, announced: AnnouncedAddress.Null);

        //Act
        var node = await GetFirstNodeAsync(server);

        //Assert
        node[0].IsNull.Should().BeTrue();
    }

    [Fact]
    public async Task empty_address_also_applies_to_ip_metadata()
    {
        //Arrange
        using var server = CreateServer(log, ClusterEndpointType.Hostname, announced: AnnouncedAddress.Empty);

        //Act
        var node = await GetFirstNodeAsync(server);

        //Assert
        ((string?)node[0]).Should().Be(Hostname);
        GetMetadata(node, "ip").Should().Be("");
    }

    [Fact]
    public async Task pre_seven_zero_server_reports_addresses_only_and_no_metadata()
    {
        // hostname preferred *and* announced, but the server predates both
        using var server = CreateServer(log, ClusterEndpointType.Hostname, version: BeforeHostnames);
        var host = GetHost(server.DefaultEndPoint, out _);

        var node = await GetFirstNodeAsync(server);

        ((string?)node[0]).Should().Be(host);
        node.Length.Should().Be(3); // the metadata element itself only exists from 7.0
    }

    [Fact]
    public async Task pre_seven_zero_server_omits_hostname_from_cluster_nodes()
    {
        using var server = CreateServer(log, ClusterEndpointType.Hostname, version: BeforeHostnames);

        await using var conn = await server.ConnectAsync(defaultOnly: true);
        var raw = await conn.GetServer(server.DefaultEndPoint).ClusterNodesRawAsync();
        Assert.NotNull(raw);
        log.WriteLine(raw);

        raw.Should().NotContain(Hostname);
    }

    [Fact]
    public async Task name_only_seeded_server_prefers_hostname_and_has_no_address()
    {
        // seeding with a DnsEndPoint is only coherent as hostname-preferred: there is no address to report
        using var server = new InProcessTestServer(log, new DnsEndPoint(Hostname, 6379))
        {
            ServerType = ServerType.Cluster,
        };
        // the preference lands on the node, not the server: seeding by name says nothing about peers
        server.TryGetNode(server.DefaultEndPoint, out var seeded).Should().BeTrue();
        seeded.PreferredEndpointType.Should().Be(ClusterEndpointType.Hostname);
        server.PreferredEndpointType.Should().Be(ClusterEndpointType.Ip);

        var node = await GetFirstNodeAsync(server);

        ((string?)node[0]).Should().Be(Hostname);
        GetMetadata(node, "ip").Should().Be("");
    }

    [Fact]
    public async Task a_hostname_preferring_node_describes_every_peer_by_name()
    {
        // an address-keyed cluster in which one node prefers hostnames - a rolling config change is
        // exactly this for a window. Verified against a real 8.9 cluster: the preference is the
        // *answering* node's and applies to every entry in its reply
        using var server = CreateServer(log, ClusterEndpointType.Ip);
        var host = GetHost(server.DefaultEndPoint, out var defaultPort);
        var outlier = server.AddEmptyNode(new IPEndPoint(IPAddress.Loopback, defaultPort + 1));
        server.Migrate((RedisKey)"outlier-slot-key", outlier);
        server.SetPreferredEndpointType(outlier, ClusterEndpointType.Hostname);

        // asked of the majority node: everything by address, the announced hostname as metadata
        var viaAddress = await GetNodesByPortAsync(server, server.DefaultEndPoint);
        ((string?)viaAddress[defaultPort][0]).Should().Be(host);
        ((string?)viaAddress[defaultPort + 1][0]).Should().Be(host);
        GetMetadata(viaAddress[defaultPort], "hostname").Should().Be(Hostname);

        // asked of the outlier: everything by name, and the peer with no hostname of its own is "?" -
        // note that includes the outlier describing itself
        var viaName = await GetNodesByPortAsync(server, outlier);
        ((string?)viaName[defaultPort][0]).Should().Be(Hostname);
        ((string?)viaName[defaultPort + 1][0]).Should().Be("?");
        GetMetadata(viaName[defaultPort], "ip").Should().Be(host);
        GetMetadata(viaName[defaultPort + 1], "ip").Should().Be(host);
    }

    [Fact]
    public async Task an_address_preferring_node_describes_every_peer_by_address()
    {
        // the mirror: a hostname-preferring cluster with one node that still reports addresses
        using var server = CreateServer(log, ClusterEndpointType.Hostname);
        var host = GetHost(server.DefaultEndPoint, out var defaultPort);
        var outlier = server.AddEmptyNode(new IPEndPoint(IPAddress.Loopback, defaultPort + 1));
        server.Migrate((RedisKey)"outlier-slot-key", outlier);
        server.SetPreferredEndpointType(outlier, ClusterEndpointType.Ip);
        server.SetHostname(outlier, "outlier.redis.example.com");

        var viaName = await GetNodesByPortAsync(server, server.DefaultEndPoint);
        ((string?)viaName[defaultPort][0]).Should().Be(Hostname);
        ((string?)viaName[defaultPort + 1][0]).Should().Be("outlier.redis.example.com");

        // the outlier reports addresses for everyone, with both hostnames moved into the metadata
        var viaAddress = await GetNodesByPortAsync(server, outlier);
        ((string?)viaAddress[defaultPort][0]).Should().Be(host);
        ((string?)viaAddress[defaultPort + 1][0]).Should().Be(host);
        GetMetadata(viaAddress[defaultPort], "hostname").Should().Be(Hostname);
        GetMetadata(viaAddress[defaultPort + 1], "hostname").Should().Be("outlier.redis.example.com");
    }

    [Fact]
    public async Task config_get_reports_the_answering_nodes_own_view()
    {
        using var server = CreateServer(log, ClusterEndpointType.Ip);
        GetHost(server.DefaultEndPoint, out var defaultPort);
        var outlier = server.AddEmptyNode(new DnsEndPoint("outlier.redis.example.com", defaultPort + 1));
        server.Migrate((RedisKey)"outlier-slot-key", outlier);

        await using var conn = await server.ConnectAsync();

        // the parameter is per-node, so each connection sees its own node's answer
        (await GetConfigAsync(conn.GetServer(server.DefaultEndPoint), "cluster-preferred-endpoint-type")).Should().Be("ip");
        (await GetConfigAsync(conn.GetServer(outlier), "cluster-preferred-endpoint-type")).Should().Be("hostname");
        (await GetConfigAsync(conn.GetServer(outlier), "cluster-announce-hostname")).Should().Be("outlier.redis.example.com");
    }

    [Theory]
    [InlineData(ClusterEndpointType.Ip, "ip")]
    [InlineData(ClusterEndpointType.Hostname, "hostname")]
    [InlineData(ClusterEndpointType.UnknownEndpoint, "unknown-endpoint")]
    public async Task config_get_reports_the_announce_settings(ClusterEndpointType preferred, string expected)
    {
        //Arrange
        using var server = CreateServer(log, preferred);
        await using var conn = await server.ConnectAsync(defaultOnly: true);

        //Act
        var api = conn.GetServer(server.DefaultEndPoint);

        //Assert
        (await GetConfigAsync(api, "cluster-preferred-endpoint-type")).Should().Be(expected);
        (await GetConfigAsync(api, "cluster-announce-hostname")).Should().Be(Hostname);
    }

    [Fact]
    public async Task config_get_omits_announce_settings_before_seven_zero()
    {
        //Arrange
        using var server = CreateServer(log, ClusterEndpointType.Hostname, version: BeforeHostnames);
        await using var conn = await server.ConnectAsync(defaultOnly: true);

        //Act
        var api = conn.GetServer(server.DefaultEndPoint);

        //Assert
        (await GetConfigAsync(api, "cluster-preferred-endpoint-type")).Should().BeNull();
        (await GetConfigAsync(api, "cluster-announce-hostname")).Should().BeNull();
    }

    private static async Task<string?> GetConfigAsync(IServer api, string key)
    {
        foreach (var pair in await api.ConfigGetAsync(key))
        {
            if (string.Equals(pair.Key, key, System.StringComparison.OrdinalIgnoreCase)) return pair.Value;
        }
        return null;
    }

    [Theory]
    [InlineData(ClusterEndpointType.Ip, true, "127.0.0.1")]
    [InlineData(ClusterEndpointType.Hostname, true, Hostname)]
    [InlineData(ClusterEndpointType.Hostname, false, "?")]
    [InlineData(ClusterEndpointType.UnknownEndpoint, true, "")]
    public async Task moved_redirect_uses_the_preferred_form(ClusterEndpointType preferred, bool announceHostname, string expectedHost)
    {
        using var server = CreateServer(log, preferred, announceHostname);

        // connect *before* migrating, so the client's cached slot map still points at the old owner
        // and the command actually earns a redirect
        await using var conn = await server.ConnectAsync(defaultOnly: true);

        var other = server.AddEmptyNode();
        if (announceHostname) server.SetHostname(other, "host-2.redis.example.com");
        server.Migrate((RedisKey)"moved-form-key", other);
        GetHost(other, out var otherPort);

        // NoRedirect so the error surfaces rather than being followed
        var ex = await Assert.ThrowsAsync<RedisServerException>(
            async () => await conn.GetDatabase().StringGetAsync("moved-form-key", CommandFlags.NoRedirect));
        log.WriteLine(ex.Message);

        var expected = preferred == ClusterEndpointType.Hostname && announceHostname
            ? "host-2.redis.example.com"
            : expectedHost;

        if (expected is "?" or "")
        {
            // nothing usable to route to; UnroutableRedirectUnitTests covers the handling, here we only
            // care that the server named the target the way its preference dictates
            ex.Kind.Should().Be(RedisErrorKind.UnknownRedirectTarget);
            ex.Message.Should().Contain($"'{expected}:{otherPort}'");
        }
        else
        {
            // the client restates the redirect in terms of the endpoint it parsed, so assert on that rather
            // than on the wire text; note a hostname form lands as a DnsEndPoint, which is the identity
            // hazard this fake exists to make reproducible
            ex.Message.Should().Contain("MOVED");
            ex.Message.Should().Contain($"{expected}:{otherPort}");
        }
    }
}
