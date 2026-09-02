using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

/// <summary>
/// The parsed view of <c>CLUSTER NODES</c>, specifically the part after the <c>@</c> that we used to
/// truncate away: the cluster bus port, the announced hostname, and any auxiliary fields. Documented form
/// is <c>ip:port@cport[,hostname[,aux-field=value]*]</c>.
/// </summary>
public class ClusterNodeParseUnitTests(ITestOutputHelper log)
{
    private const string Hostname = "host-1.redis.example.com";
    private static readonly EndPoint Origin = new IPEndPoint(IPAddress.Loopback, 7000);

    private static ClusterNode Parse(string line)
    {
        var config = new ClusterConfiguration(serverSelectionStrategy: null!, line, Origin);
        return config.Nodes.Single();
    }

    private const string Flags = " myself,master - 0 0 1 connected 0-16383";

    [Fact]
    public void bus_port_is_parsed()
    {
        var node = Parse("abc 127.0.0.1:7000@17000" + Flags);

        node.EndPoint.Should().Be(new IPEndPoint(IPAddress.Loopback, 7000));
        node.ClusterBusPort.Should().Be(17000);
        node.Hostname.Should().BeNull();
        node.AuxFields.Should().BeEmpty();
    }

    [Fact]
    public void pre_four_zero_line_has_no_bus_port()
    {
        // servers older than 4.0 report no "@cport" at all
        var node = Parse("abc 127.0.0.1:7000" + Flags);

        node.EndPoint.Should().Be(new IPEndPoint(IPAddress.Loopback, 7000));
        node.ClusterBusPort.Should().BeNull();
        node.Hostname.Should().BeNull();
        node.AuxFields.Should().BeEmpty();
    }

    [Fact]
    public void hostname_is_parsed()
    {
        var node = Parse($"abc 127.0.0.1:7000@17000,{Hostname}" + Flags);

        node.ClusterBusPort.Should().Be(17000);
        node.Hostname.Should().Be(Hostname);

        // the hostname is an additional identity, not a replacement for the address
        node.EndPoint.Should().Be(new IPEndPoint(IPAddress.Loopback, 7000));
    }

    [Fact]
    public void aux_fields_are_parsed()
    {
        var node = Parse($"abc 127.0.0.1:7000@17000,{Hostname},shard-id=abc123,human-nodename=alpha" + Flags);

        node.Hostname.Should().Be(Hostname);
        Assert.Collection(
            node.AuxFields,
            x => x.Should().Be(new KeyValuePair<string, string>("shard-id", "abc123")),
            x => x.Should().Be(new KeyValuePair<string, string>("human-nodename", "alpha")));
    }

    [Fact]
    public void aux_fields_survive_an_empty_hostname_slot()
    {
        // the hostname slot is positional, so it can be empty while aux fields follow it
        var node = Parse("abc 127.0.0.1:7000@17000,,shard-id=abc123" + Flags);

        node.Hostname.Should().BeNull();
        Assert.Single(node.AuxFields).Should().Be(new KeyValuePair<string, string>("shard-id", "abc123"));
    }

    [Fact]
    public void unrecognized_aux_fields_are_preserved()
    {
        // the set is documented as extensible, so a field we have never heard of must round-trip
        var node = Parse($"abc 127.0.0.1:7000@17000,{Hostname},not-invented-yet=42" + Flags);

        Assert.Single(node.AuxFields).Should().Be(new KeyValuePair<string, string>("not-invented-yet", "42"));
    }

    [Fact]
    public void malformed_trailer_does_not_throw()
    {
        // an exception here does silent damage to topology, so parsing is deliberately lenient
        var node = Parse($"abc 127.0.0.1:7000@not-a-port,{Hostname},no-equals-sign,=novalue,k=v" + Flags);

        node.ClusterBusPort.Should().BeNull();
        node.Hostname.Should().Be(Hostname);
        Assert.Single(node.AuxFields).Should().Be(new KeyValuePair<string, string>("k", "v"));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task round_trips_through_the_server(bool announceHostname, bool auxFields)
    {
        using var server = new InProcessTestServer(log) { ServerType = ServerType.Cluster };
        var endpoint = server.DefaultEndPoint;
        if (announceHostname) server.SetHostname(endpoint, Hostname);
        if (auxFields) server.SetAuxField(endpoint, "shard-id", "abc123");

        await using var conn = await server.ConnectAsync(defaultOnly: true);
        var config = await conn.GetServer(endpoint).ClusterNodesAsync();
        Assert.NotNull(config);
        var node = config.Nodes.Single();
        log.WriteLine(node.Raw);

        TestServer.RedisServer.GetHost(endpoint, out var port);
        node.ClusterBusPort.Should().Be(port + 10000);
        node.Hostname.Should().Be(announceHostname ? Hostname : null);
        if (auxFields)
        {
            Assert.Single(node.AuxFields).Should().Be(new KeyValuePair<string, string>("shard-id", "abc123"));
        }
        else
        {
            node.AuxFields.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task pre_seven_zero_server_reports_no_hostname()
    {
        using var server = new InProcessTestServer(log)
        {
            ServerType = ServerType.Cluster,
            RedisVersion = new System.Version(6, 2, 0),
        };
        server.SetHostname(server.DefaultEndPoint, Hostname);

        await using var conn = await server.ConnectAsync(defaultOnly: true);
        var config = await conn.GetServer(server.DefaultEndPoint).ClusterNodesAsync();
        Assert.NotNull(config);
        var node = config.Nodes.Single();
        node.Hostname.Should().BeNull();
        node.ClusterBusPort.Should().Be(6379 + 10000); // the cport predates hostnames
    }
}
