using System.Net;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

public class SentinelGetReplicaAddresses(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    [Fact]
    public void single_replica_success()
    {
        //Arrange
        var resp = "*1\r\n*4\r\n$2\r\nip\r\n$9\r\n127.0.0.1\r\n$4\r\nport\r\n$4\r\n6380\r\n";

        //Act
        var result = Execute(resp, ResultProcessor.SentinelReplicaEndPoints);

        //Assert
        Assert.NotNull(result);
        result.Should().ContainSingle();

        var endpoint = Assert.IsType<System.Net.IPEndPoint>(result[0]);
        endpoint.Address.ToString().Should().Be("127.0.0.1");
        endpoint.Port.Should().Be(6380);
    }

    [Fact]
    public void multiple_replicas_success()
    {
        //Arrange
        var resp = "*2\r\n*4\r\n$2\r\nip\r\n$9\r\n127.0.0.1\r\n$4\r\nport\r\n$4\r\n6380\r\n*4\r\n$2\r\nip\r\n$9\r\n127.0.0.2\r\n$4\r\nport\r\n$4\r\n6381\r\n";

        //Act
        var result = Execute(resp, ResultProcessor.SentinelReplicaEndPoints);

        //Assert
        Assert.NotNull(result);
        result.Length.Should().Be(2);

        var endpoint1 = Assert.IsType<System.Net.IPEndPoint>(result[0]);
        endpoint1.Address.ToString().Should().Be("127.0.0.1");
        endpoint1.Port.Should().Be(6380);

        var endpoint2 = Assert.IsType<System.Net.IPEndPoint>(result[1]);
        endpoint2.Address.ToString().Should().Be("127.0.0.2");
        endpoint2.Port.Should().Be(6381);
    }

    [Fact]
    public void dns_endpoint_success()
    {
        //Arrange
        var resp = "*1\r\n*4\r\n$2\r\nip\r\n$17\r\nredis.example.com\r\n$4\r\nport\r\n$4\r\n6380\r\n";

        //Act
        var result = Execute(resp, ResultProcessor.SentinelReplicaEndPoints);

        //Assert
        Assert.NotNull(result);
        result.Should().ContainSingle();

        var endpoint = Assert.IsType<System.Net.DnsEndPoint>(result[0]);
        endpoint.Host.Should().Be("redis.example.com");
        endpoint.Port.Should().Be(6380);
    }

    [Fact]
    public void reversed_order_success()
    {
        // Test that order doesn't matter - port before ip
        var resp = "*1\r\n*4\r\n$4\r\nport\r\n$4\r\n6380\r\n$2\r\nip\r\n$9\r\n127.0.0.1\r\n";
        var result = Execute(resp, ResultProcessor.SentinelReplicaEndPoints);

        Assert.NotNull(result);
        result.Should().ContainSingle();
        var endpoint = Assert.IsType<IPEndPoint>(result[0]);
        endpoint.Address.ToString().Should().Be("127.0.0.1");
        endpoint.Port.Should().Be(6380);
    }

    [Fact]
    public void empty_array_failure()
    {
        var resp = "*0\r\n";
        ExecuteUnexpected(resp, ResultProcessor.SentinelReplicaEndPoints);
    }

    [Fact]
    public void null_array_failure()
    {
        var resp = "*-1\r\n";
        ExecuteUnexpected(resp, ResultProcessor.SentinelReplicaEndPoints);
    }

    [Fact]
    public void not_array_failure()
    {
        var resp = "+OK\r\n";
        ExecuteUnexpected(resp, ResultProcessor.SentinelReplicaEndPoints);
    }
}
