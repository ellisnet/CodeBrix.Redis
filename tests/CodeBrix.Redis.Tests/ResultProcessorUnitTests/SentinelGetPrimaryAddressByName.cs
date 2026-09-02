using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

public class SentinelGetPrimaryAddressByName(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    [Fact]
    public void valid_host_and_port_success()
    {
        // Array with 2 elements: host (bulk string) and port (integer)
        var resp = "*2\r\n$9\r\n127.0.0.1\r\n:6379\r\n";
        var result = Execute(resp, ResultProcessor.SentinelPrimaryEndpoint);

        result.Should().NotBeNull();
        var ipEndpoint = Assert.IsType<System.Net.IPEndPoint>(result);
        ipEndpoint.Address.ToString().Should().Be("127.0.0.1");
        ipEndpoint.Port.Should().Be(6379);
    }

    [Fact]
    public void domain_name_and_port_success()
    {
        // Array with 2 elements: domain name (bulk string) and port (integer)
        var resp = "*2\r\n$17\r\nredis.example.com\r\n:6380\r\n";
        var result = Execute(resp, ResultProcessor.SentinelPrimaryEndpoint);

        result.Should().NotBeNull();
        var dnsEndpoint = Assert.IsType<System.Net.DnsEndPoint>(result);
        dnsEndpoint.Host.Should().Be("redis.example.com");
        dnsEndpoint.Port.Should().Be(6380);
    }

    [Fact]
    public void null_array_success()
    {
        // Null array - primary doesn't exist
        var resp = "*-1\r\n";
        var result = Execute(resp, ResultProcessor.SentinelPrimaryEndpoint);

        result.Should().BeNull();
    }

    [Fact]
    public void empty_array_success()
    {
        // Empty array - primary doesn't exist
        var resp = "*0\r\n";
        var result = Execute(resp, ResultProcessor.SentinelPrimaryEndpoint);

        result.Should().BeNull();
    }

    [Fact]
    public void not_array_failure()
    {
        // Simple string instead of array
        var resp = "+OK\r\n";
        ExecuteUnexpected(resp, ResultProcessor.SentinelPrimaryEndpoint);
    }

    [Fact]
    public void array_with_one_element_failure()
    {
        // Array with only 1 element (missing port)
        var resp = "*1\r\n$9\r\n127.0.0.1\r\n";
        ExecuteUnexpected(resp, ResultProcessor.SentinelPrimaryEndpoint);
    }

    [Fact]
    public void array_with_three_elements_failure()
    {
        // Array with 3 elements (too many)
        var resp = "*3\r\n$9\r\n127.0.0.1\r\n:6379\r\n$5\r\nextra\r\n";
        ExecuteUnexpected(resp, ResultProcessor.SentinelPrimaryEndpoint);
    }

    [Fact]
    public void array_with_non_integer_port_failure()
    {
        // Array with 2 elements but port is not an integer
        var resp = "*2\r\n$9\r\n127.0.0.1\r\n$4\r\nport\r\n";
        ExecuteUnexpected(resp, ResultProcessor.SentinelPrimaryEndpoint);
    }
}
