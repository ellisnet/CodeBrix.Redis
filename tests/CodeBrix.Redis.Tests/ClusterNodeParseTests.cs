using System;
using System.Linq;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

/// <summary>
/// End-to-end coverage for the parsed <c>CLUSTER NODES</c> members against a real cluster; the deterministic
/// grammar cases live in <see cref="ClusterNodeParseUnitTests"/>. Runs per protocol deliberately: under RESP3
/// this reply arrives as a verbatim string with a <c>txt:</c> prefix, which is its own parsing hazard.
/// </summary>
[RunPerProtocol]
public class ClusterNodeParseTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    protected override string GetConfiguration() => TestConfig.Current.ClusterServersAndPorts + ",connectTimeout=10000";

    private static readonly Version BusPortVersion = new(4, 0, 0);   // "@cport" arrives here
    private static readonly Version HostnameVersion = new(7, 0, 0);  // hostnames arrive here

    /// <summary>The endpoint token's trailer - "cport[,hostname[,aux=value]*]" - taken straight from the raw line.</summary>
    private static string? GetRawTrailer(ClusterNode node)
    {
        var endpointToken = node.Raw.Split(' ')[1];
        var at = endpointToken.IndexOf('@');
        return at < 0 ? null : endpointToken.Substring(at + 1);
    }

    [Fact]
    public async Task parsed_members_agree_with_the_raw_line()
    {
        await using var conn = Create(allowAdmin: true);
        var api = conn.GetServer(conn.GetEndPoints()[0]);
        Assert.SkipUnless(api.Version >= BusPortVersion, $"cluster bus port needs {BusPortVersion}, server is {api.Version}");

        var config = await api.ClusterNodesAsync();
        Assert.NotNull(config);
        config.Nodes.Should().NotBeEmpty();

        foreach (var node in config.Nodes)
        {
            Log(node.Raw);
            var trailer = GetRawTrailer(node);
            Assert.NotNull(trailer); // 4.0+, so every line carries one (and xunit's form narrows the null-state)

            var fields = trailer.Split(',');
            node.ClusterBusPort.Should().Be(int.Parse(fields[0]));

            // whatever this deployment announces, the parsed view must agree with the text
            var expectedHostname = fields.Length > 1 && fields[1].Length > 0 ? fields[1] : null;
            node.Hostname.Should().Be(expectedHostname);
            node.AuxFields.Count.Should().Be(Math.Max(fields.Length - 2, 0));
        }
    }

    [Fact]
    public async Task bus_port_follows_the_conventional_offset()
    {
        await using var conn = Create(allowAdmin: true);
        var api = conn.GetServer(conn.GetEndPoints()[0]);
        Assert.SkipUnless(api.Version >= BusPortVersion, $"cluster bus port needs {BusPortVersion}, server is {api.Version}");

        var config = await api.ClusterNodesAsync();
        Assert.NotNull(config);
        foreach (var node in config.Nodes)
        {
            // not guaranteed by the protocol, but it is what an unconfigured cluster does, and it is the
            // assumption the toy server encodes - so worth asserting against reality rather than inferring
            var port = node.EndPoint switch
            {
                System.Net.IPEndPoint ip => ip.Port,
                System.Net.DnsEndPoint dns => dns.Port,
                _ => 0,
            };
            Log($"{node.EndPoint} bus {node.ClusterBusPort}");
            node.ClusterBusPort.Should().Be(port + 10000);
        }
    }

    [Fact]
    public async Task announced_hostname_is_parsed()
    {
        await using var conn = Create(allowAdmin: true);
        var endpoint = conn.GetEndPoints()[0];
        var api = conn.GetServer(endpoint);
        Assert.SkipUnless(api.Version >= HostnameVersion, $"hostnames need {HostnameVersion}, server is {api.Version}");

        const string Hostname = "se-redis-test.example.com";
        const string Key = "cluster-announce-hostname";

        var before = (await api.ConfigGetAsync(Key)).SingleOrDefault().Value ?? "";
        Log($"restoring '{Key}' to '{before}' afterwards");
        try
        {
            await api.ConfigSetAsync(Key, Hostname);

            var config = await api.ClusterNodesAsync();
            Assert.NotNull(config);
            // the node we asked is the one that announced it; peers learn it by gossip, so do not race on them
            var self = Assert.Single(config.Nodes, x => x.IsMyself);
            Log(self.Raw);
            self.Hostname.Should().Be(Hostname);

            // and it remains an *additional* identity - the endpoint is still the address
            self.EndPoint.Should().Be(endpoint);
        }
        finally
        {
            await api.ConfigSetAsync(Key, before);
        }
    }
}
