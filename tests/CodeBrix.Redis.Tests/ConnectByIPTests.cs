using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class ConnectByIPTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public void parse_endpoints()
    {
        var eps = new EndPointCollection
        {
            { "127.0.0.1", 1000 },
            { "::1", 1001 },
            { "localhost", 1002 },
        };

        eps[0].AddressFamily.Should().Be(AddressFamily.InterNetwork);
        eps[1].AddressFamily.Should().Be(AddressFamily.InterNetworkV6);
        eps[2].AddressFamily.Should().Be(AddressFamily.Unspecified);

        eps[0].ToString().Should().Be("127.0.0.1:1000");
        eps[1].ToString().Should().Be("[::1]:1001");
        eps[2].ToString().Should().Be("Unspecified/localhost:1002");
    }

    [Fact]
    public async Task i_pv4_connection()
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        var config = new ConfigurationOptions
        {
            EndPoints = { { TestConfig.Current.IPv4Server, TestConfig.Current.IPv4Port } },
        };
        await using var conn = ConnectionMultiplexer.Connect(config);

        var server = conn.GetServer(config.EndPoints[0]);
        server.EndPoint.AddressFamily.Should().Be(AddressFamily.InterNetwork);
        await server.PingAsync();
    }

    [Fact]
    public async Task i_pv6_connection()
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        var config = new ConfigurationOptions
        {
            EndPoints = { { TestConfig.Current.IPv6Server, TestConfig.Current.IPv6Port } },
        };
        await using var conn = ConnectionMultiplexer.Connect(config);

        var server = conn.GetServer(config.EndPoints[0]);
        server.EndPoint.AddressFamily.Should().Be(AddressFamily.InterNetworkV6);
        await server.PingAsync();
    }

    [Theory]
    [MemberData(nameof(ConnectByVariousEndpointsData))]
    public async Task connect_by_various_endpoints(EndPoint ep, AddressFamily expectedFamily)
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        ep.AddressFamily.Should().Be(expectedFamily);
        var config = new ConfigurationOptions
        {
            EndPoints = { ep },
        };
        if (ep.AddressFamily != AddressFamily.InterNetworkV6) // I don't have IPv6 servers
        {
            await using (var conn = ConnectionMultiplexer.Connect(config))
            {
                var actual = conn.GetEndPoints().Single();
                var server = conn.GetServer(actual);
                await server.PingAsync();
            }
        }
    }

    public static IEnumerable<object[]> ConnectByVariousEndpointsData()
    {
        yield return new object[] { new IPEndPoint(IPAddress.Loopback, 6379), AddressFamily.InterNetwork };

        yield return new object[] { new IPEndPoint(IPAddress.IPv6Loopback, 6379), AddressFamily.InterNetworkV6 };

        yield return new object[] { new DnsEndPoint("localhost", 6379), AddressFamily.Unspecified };

        yield return new object[] { new DnsEndPoint("localhost", 6379, AddressFamily.InterNetwork), AddressFamily.InterNetwork };

        yield return new object[] { new DnsEndPoint("localhost", 6379, AddressFamily.InterNetworkV6), AddressFamily.InterNetworkV6 };

        yield return new object[] { ConfigurationOptions.Parse("localhost:6379").EndPoints.Single(), AddressFamily.Unspecified };

        yield return new object[] { ConfigurationOptions.Parse("localhost").EndPoints.Single(), AddressFamily.Unspecified };

        yield return new object[] { ConfigurationOptions.Parse("127.0.0.1:6379").EndPoints.Single(), AddressFamily.InterNetwork };

        yield return new object[] { ConfigurationOptions.Parse("127.0.0.1").EndPoints.Single(), AddressFamily.InterNetwork };

        yield return new object[] { ConfigurationOptions.Parse("[::1]").EndPoints.Single(), AddressFamily.InterNetworkV6 };

        yield return new object[] { ConfigurationOptions.Parse("[::1]:6379").EndPoints.Single(), AddressFamily.InterNetworkV6 };
    }
}
