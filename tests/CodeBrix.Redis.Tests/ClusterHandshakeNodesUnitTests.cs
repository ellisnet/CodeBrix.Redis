using System.Linq;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

// context: https://github.com/StackExchange/StackExchange.Redis/pull/3043
public class ClusterHandshakeNodesUnitTests(ITestOutputHelper log)
{
    [Fact]
    public async Task cluster_handshake_nodes_are_ignored()
    {
        using var server = new InProcessTestServer() { ServerType = ServerType.Cluster };
        var a = server.DefaultEndPoint;
        var b = server.AddEmptyNode();
        var c = server.AddEmptyNode(TestServer.RedisServer.NodeFlags.Handshake);
        await using var conn = await server.ConnectAsync(defaultOnly: true); // defaultOnly: only connect to a initially

        log.WriteLine($"a: {Format.ToString(a)}, b: {Format.ToString(b)}, c: {Format.ToString(c)}");
        var ep = conn.GetEndPoints();
        log.WriteLine("Endpoints:");
        foreach (var e in ep)
        {
            log.WriteLine(Format.ToString(e));
        }
        ep.Length.Should().Be(2);
        ep.Should().Contain(a);
        ep.Should().Contain(b);
        ep.Should().NotContain(c);
    }

    [Fact]
    public async Task cluster_handshake_nodes_are_not_ignored_when_fetching_directly()
    {
        using var server = new InProcessTestServer() { ServerType = ServerType.Cluster };
        var a = server.DefaultEndPoint;
        var b = server.AddEmptyNode();
        var c = server.AddEmptyNode(TestServer.RedisServer.NodeFlags.Handshake);
        await using var conn = await server.ConnectAsync(defaultOnly: true); // defaultOnly: only connect to a initially

        // check we can still *fetch* handshake nodes via the admin API
        var serverApi = conn.GetServer(a);
        var config = await serverApi.ClusterNodesAsync();
        Assert.NotNull(config);
        config.Nodes.Count.Should().Be(3);
        var eps = config.Nodes.Select(x => x.EndPoint).ToArray();
        eps.Should().Contain(a);
        eps.Should().Contain(b);
        eps.Should().Contain(c);

        config[a]!.IsHandshake.Should().BeFalse();
        config[b]!.IsHandshake.Should().BeFalse();
        config[c]!.IsHandshake.Should().BeTrue();
    }
}
