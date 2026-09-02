using System.Linq;
using System.Net;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class DefaultPortsTests
{
    [Theory]
    [InlineData("foo", 6379)]
    [InlineData("foo:6379", 6379)]
    [InlineData("foo:6380", 6380)]
    [InlineData("foo,ssl=false", 6379)]
    [InlineData("foo:6379,ssl=false", 6379)]
    [InlineData("foo:6380,ssl=false", 6380)]

    [InlineData("foo,ssl=true", 6380)]
    [InlineData("foo:6379,ssl=true", 6379)]
    [InlineData("foo:6380,ssl=true", 6380)]
    [InlineData("foo:6381,ssl=true", 6381)]
    public void config_string_round_trip_with_default_ports(string config, int expectedPort)
    {
        var options = ConfigurationOptions.Parse(config);
        string backAgain = options.ToString();
        backAgain.Replace("=True", "=true").Replace("=False", "=false").Should().Be(config);

        options.SetDefaultPorts(); // normally it is the multiplexer that calls this, not us
        (((DnsEndPoint)options.EndPoints.Single()).Port).Should().Be(expectedPort);
    }

    [Theory]
    [InlineData("foo", 0, false, 6379)]
    [InlineData("foo", 6379, false, 6379)]
    [InlineData("foo", 6380, false, 6380)]

    [InlineData("foo", 0, true, 6380)]
    [InlineData("foo", 6379, true, 6379)]
    [InlineData("foo", 6380, true, 6380)]
    [InlineData("foo", 6381, true, 6381)]

    public void config_manual_with_default_ports(string host, int port, bool useSsl, int expectedPort)
    {
        var options = new ConfigurationOptions();
        if (port == 0)
        {
            options.EndPoints.Add(host);
        }
        else
        {
            options.EndPoints.Add(host, port);
        }
        if (useSsl) options.Ssl = true;

        options.SetDefaultPorts(); // normally it is the multiplexer that calls this, not us
        (((DnsEndPoint)options.EndPoints.Single()).Port).Should().Be(expectedPort);
    }
}
