using System.Net;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

/// <summary>
/// Coverage for a node having more than one identity - the enabler for the endpoint-identity work
/// (#2826), and a prerequisite for reacting to endpoints we did not choose the form of.
/// </summary>
public class EndpointIdentityUnitTests(ITestOutputHelper log)
{
    private const string Hostname = "host-1.redis.example.com";

    private static DnsEndPoint AliasFor(EndPoint endpoint, string host = Hostname)
    {
        TestServer.RedisServer.GetHost(endpoint, out var port);
        return new DnsEndPoint(host, port);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task node_is_reachable_by_alias(bool useSsl)
    {
        using var server = new InProcessTestServer(log, useSsl: useSsl);
        var canonical = server.DefaultEndPoint; // an IPEndPoint
        var alias = AliasFor(canonical);
        server.AddAlias(alias, canonical);

        // dial the *alias*; before the alias map this fell through to a real socket connect
        var config = server.GetClientConfig(defaultOnly: true);
        config.EndPoints.Clear();
        config.EndPoints.Add(alias);

        await using var conn = await ConnectionMultiplexer.ConnectAsync(config);
        var db = conn.GetDatabase();
        await db.StringSetAsync(nameof(node_is_reachable_by_alias), "abc");

        (await db.StringGetAsync(nameof(node_is_reachable_by_alias))).Should().Be("abc");
        Assert.Single(conn.GetEndPoints()).Should().Be(alias);
    }

    [Fact]
    public async Task alias_and_canonical_endpoint_see_the_same_data()
    {
        using var server = new InProcessTestServer(log);
        var canonical = server.DefaultEndPoint;
        var alias = AliasFor(canonical);
        server.AddAlias(alias, canonical);

        var viaCanonical = server.GetClientConfig(defaultOnly: true);
        var viaAlias = server.GetClientConfig(defaultOnly: true);
        viaAlias.EndPoints.Clear();
        viaAlias.EndPoints.Add(alias);

        await using var byIp = await ConnectionMultiplexer.ConnectAsync(viaCanonical);
        await using var byName = await ConnectionMultiplexer.ConnectAsync(viaAlias);

        await byIp.GetDatabase().StringSetAsync(nameof(alias_and_canonical_endpoint_see_the_same_data), "xyz");

        // one node, two names: the value written via one identity is visible via the other
        (await byName.GetDatabase().StringGetAsync(nameof(alias_and_canonical_endpoint_see_the_same_data))).Should().Be("xyz");
    }

    [Fact]
    public void set_hostname_registers_the_name_as_an_alias()
    {
        using var server = new InProcessTestServer(log);
        var canonical = server.DefaultEndPoint;
        server.SetHostname(canonical, Hostname);

        server.TryGetNode(AliasFor(canonical), out var byName).Should().BeTrue();
        server.TryGetNode(canonical, out var byAddress).Should().BeTrue();
        byName.Should().BeSameAs(byAddress);

        // GetEndPoints stays one-per-node; the alias is reported separately
        Assert.Single(server.GetEndPoints()).Should().Be(canonical);
        Assert.Single(server.GetAliases()).Should().Be(AliasFor(canonical));
    }

    [Fact]
    public async Task cluster_nodes_announces_hostname()
    {
        // hostnames need a 7.0+ server, which the in-process server declares by default
        using var server = new InProcessTestServer(log) { ServerType = ServerType.Cluster };
        var canonical = server.DefaultEndPoint;
        server.SetHostname(canonical, Hostname);

        await using var conn = await server.ConnectAsync(defaultOnly: true);
        var raw = await conn.GetServer(canonical).ClusterNodesRawAsync();
        Assert.NotNull(raw);
        log.WriteLine(raw);

        var host = TestServer.RedisServer.GetHost(canonical, out var port);

        // <id> <ip:port@cport,hostname> ...
        raw.Should().Contain($"{host}:{port}@{port + 10000},{Hostname} ");
    }

    [Fact]
    public async Task cluster_nodes_omits_hostname_when_none_announced()
    {
        using var server = new InProcessTestServer(log) { ServerType = ServerType.Cluster };
        var canonical = server.DefaultEndPoint;

        await using var conn = await server.ConnectAsync(defaultOnly: true);
        var raw = await conn.GetServer(canonical).ClusterNodesRawAsync();
        Assert.NotNull(raw);
        log.WriteLine(raw);

        var host = TestServer.RedisServer.GetHost(canonical, out var port);
        raw.Should().Contain($"{host}:{port}@{port + 10000} ");

        // no trailing hostname on the endpoint token (the flags field has commas of its own)
        raw.Should().NotContain($"@{port + 10000},");
    }

    [Fact]
    public async Task tls_name_mismatch_is_not_forgiven()
    {
        // the in-process certificate covers every identity the node can be dialled by, so a mismatch
        // has to be forced; this asserts the validation callback no longer waves one through on
        // thumbprint alone, which is what would let the SslHost handling regress unnoticed
        using var server = new InProcessTestServer(log, useSsl: true);
        var config = server.GetClientConfig(defaultOnly: true);
        config.SslHost = "not-in-the-certificate.example.com";
        config.ConnectTimeout = 2000;
        config.ConnectRetry = 1;

        var ex = await Assert.ThrowsAnyAsync<RedisConnectionException>(
            async () => await ConnectionMultiplexer.ConnectAsync(config));
        log.WriteLine(ex.Message);
    }
}
