using System.Net;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

public class SentinelGetSentinelAddresses(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    [Fact]
    public void single_sentinel_success()
    {
        //Arrange
        var resp = "*1\r\n*4\r\n$2\r\nip\r\n$9\r\n127.0.0.1\r\n$4\r\nport\r\n$5\r\n26379\r\n";

        //Act
        var result = Execute(resp, ResultProcessor.SentinelAddressesEndPoints);

        //Assert
        Assert.NotNull(result);
        result.Should().ContainSingle();
        var endpoint = Assert.IsType<IPEndPoint>(result[0]);
        endpoint.Address.ToString().Should().Be("127.0.0.1");
        endpoint.Port.Should().Be(26379);
    }

    [Fact]
    public void multiple_sentinels_success()
    {
        //Arrange
        var resp = "*2\r\n*4\r\n$2\r\nip\r\n$9\r\n127.0.0.1\r\n$4\r\nport\r\n$5\r\n26379\r\n*4\r\n$2\r\nip\r\n$9\r\n127.0.0.2\r\n$4\r\nport\r\n$5\r\n26380\r\n";

        //Act
        var result = Execute(resp, ResultProcessor.SentinelAddressesEndPoints);

        //Assert
        Assert.NotNull(result);
        result.Length.Should().Be(2);

        var endpoint1 = Assert.IsType<IPEndPoint>(result[0]);
        endpoint1.Address.ToString().Should().Be("127.0.0.1");
        endpoint1.Port.Should().Be(26379);

        var endpoint2 = Assert.IsType<IPEndPoint>(result[1]);
        endpoint2.Address.ToString().Should().Be("127.0.0.2");
        endpoint2.Port.Should().Be(26380);
    }

    [Fact]
    public void dns_endpoint_success()
    {
        //Arrange
        var resp = "*1\r\n*4\r\n$2\r\nip\r\n$20\r\nsentinel.example.com\r\n$4\r\nport\r\n$5\r\n26379\r\n";

        //Act
        var result = Execute(resp, ResultProcessor.SentinelAddressesEndPoints);

        //Assert
        Assert.NotNull(result);
        result.Should().ContainSingle();
        var endpoint = Assert.IsType<DnsEndPoint>(result[0]);
        endpoint.Host.Should().Be("sentinel.example.com");
        endpoint.Port.Should().Be(26379);
    }

    [Fact]
    public void reversed_order_success()
    {
        //Arrange
        var resp = "*1\r\n*4\r\n$4\r\nport\r\n$5\r\n26379\r\n$2\r\nip\r\n$9\r\n127.0.0.1\r\n";

        //Act
        var result = Execute(resp, ResultProcessor.SentinelAddressesEndPoints);

        //Assert
        Assert.NotNull(result);
        result.Should().ContainSingle();
        var endpoint = Assert.IsType<IPEndPoint>(result[0]);
        endpoint.Address.ToString().Should().Be("127.0.0.1");
        endpoint.Port.Should().Be(26379);
    }

    [Fact]
    public void empty_array_success()
    {
        //Arrange
        var resp = "*0\r\n";

        //Act
        var result = Execute(resp, ResultProcessor.SentinelAddressesEndPoints);

        //Assert
        Assert.NotNull(result);
        result.Should().BeEmpty();
    }

    [Fact]
    public void null_bulk_string_failure()
    {
        //Arrange
        var resp = "$-1\r\n";

        //Act
        var success = TryExecute(resp, ResultProcessor.SentinelAddressesEndPoints, out var result, out var exception);

        //Assert
        success.Should().BeFalse();
    }

    [Fact]
    public void not_array_failure()
    {
        var resp = "+OK\r\n";
        ExecuteUnexpected(resp, ResultProcessor.SentinelAddressesEndPoints);
    }
}
