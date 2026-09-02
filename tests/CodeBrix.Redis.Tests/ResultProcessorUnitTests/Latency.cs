using System;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

/// <summary>
/// Tests for Latency result processors.
/// </summary>
public class Latency(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    [Theory]
    [InlineData("*0\r\n", 0)] // empty array
    [InlineData("*1\r\n*4\r\n$7\r\ncommand\r\n:1405067976\r\n:251\r\n:1001\r\n", 1)] // single entry
    [InlineData("*2\r\n*4\r\n$7\r\ncommand\r\n:1405067976\r\n:251\r\n:1001\r\n*4\r\n$4\r\nfast\r\n:1405067980\r\n:100\r\n:500\r\n", 2)] // two entries
    public void latency_latest_entry_valid_input(string resp, int expectedCount)
    {
        //Arrange
        var processor = LatencyLatestEntry.ToArray;

        //Act
        var result = Execute(resp, processor);

        //Assert
        Assert.NotNull(result);
        result.Length.Should().Be(expectedCount);
    }

    [Fact]
    public void latency_latest_entry_validates_content()
    {
        // Single entry: ["command", 1405067976, 251, 1001]
        var resp = "*1\r\n*4\r\n$7\r\ncommand\r\n:1405067976\r\n:251\r\n:1001\r\n";
        var processor = LatencyLatestEntry.ToArray;
        var result = Execute(resp, processor);

        Assert.NotNull(result);
        result.Should().ContainSingle();

        var entry = result[0];
        entry.EventName.Should().Be("command");
        entry.Timestamp.Should().Be(RedisBase.UnixEpoch.AddSeconds(1405067976));
        entry.DurationMilliseconds.Should().Be(251);
        entry.MaxDurationMilliseconds.Should().Be(1001);
    }

    [Theory]
    [InlineData("*-1\r\n")] // null array (RESP2)
    [InlineData("_\r\n")] // null (RESP3)
    public void latency_latest_entry_null_array(string resp)
    {
        //Arrange
        var processor = LatencyLatestEntry.ToArray;

        //Act
        var result = Execute(resp, processor);

        //Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("*0\r\n", 0)] // empty array
    [InlineData("*1\r\n*2\r\n:1405067822\r\n:251\r\n", 1)] // single entry
    [InlineData("*2\r\n*2\r\n:1405067822\r\n:251\r\n*2\r\n:1405067941\r\n:1001\r\n", 2)] // two entries (from redis-cli example)
    public void latency_history_entry_valid_input(string resp, int expectedCount)
    {
        //Arrange
        var processor = LatencyHistoryEntry.ToArray;

        //Act
        var result = Execute(resp, processor);

        //Assert
        Assert.NotNull(result);
        result.Length.Should().Be(expectedCount);
    }

    [Fact]
    public void latency_history_entry_validates_content()
    {
        // Two entries from redis-cli example
        var resp = "*2\r\n*2\r\n:1405067822\r\n:251\r\n*2\r\n:1405067941\r\n:1001\r\n";
        var processor = LatencyHistoryEntry.ToArray;
        var result = Execute(resp, processor);

        Assert.NotNull(result);
        result.Length.Should().Be(2);

        var entry1 = result[0];
        entry1.Timestamp.Should().Be(RedisBase.UnixEpoch.AddSeconds(1405067822));
        entry1.DurationMilliseconds.Should().Be(251);

        var entry2 = result[1];
        entry2.Timestamp.Should().Be(RedisBase.UnixEpoch.AddSeconds(1405067941));
        entry2.DurationMilliseconds.Should().Be(1001);
    }

    [Theory]
    [InlineData("*-1\r\n")] // null array (RESP2)
    [InlineData("_\r\n")] // null (RESP3)
    public void latency_history_entry_null_array(string resp)
    {
        //Arrange
        var processor = LatencyHistoryEntry.ToArray;

        //Act
        var result = Execute(resp, processor);

        //Assert
        result.Should().BeNull();
    }
}
