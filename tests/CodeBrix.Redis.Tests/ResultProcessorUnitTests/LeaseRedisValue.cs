using System;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

/// <summary>
/// Tests for LeaseRedisValue result processor.
/// </summary>
public class LeaseRedisValue(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    [Theory]
    [InlineData("*0\r\n", 0)] // empty array (key doesn't exist)
    [InlineData("*1\r\n$5\r\nhello\r\n", 1)] // array with 1 element
    [InlineData("*3\r\n$3\r\naaa\r\n$3\r\nbbb\r\n$3\r\nccc\r\n", 3)] // array with 3 elements in lexicographical order
    [InlineData("*2\r\n$4\r\ntest\r\n$5\r\nvalue\r\n", 2)] // array with 2 elements
    [InlineData("*?\r\n$5\r\nhello\r\n$5\r\nworld\r\n.\r\n", 2)] // streaming aggregate with 2 elements
    [InlineData("*?\r\n.\r\n", 0)] // streaming empty array
    public void lease_redis_value_processor_valid_input(string resp, int expectedCount)
    {
        //Arrange
        var processor = ResultProcessor.LeaseRedisValue;

        //Act
        using var result = Execute(resp, processor);

        //Assert
        Assert.NotNull(result);
        result.Length.Should().Be(expectedCount);
    }

    [Fact]
    public void lease_redis_value_processor_validates_content()
    {
        // Array of 3 RedisValues: "aaa", "bbb", "ccc"
        var resp = "*3\r\n$3\r\naaa\r\n$3\r\nbbb\r\n$3\r\nccc\r\n";
        var processor = ResultProcessor.LeaseRedisValue;
        using var result = Execute(resp, processor);

        Assert.NotNull(result);
        result.Length.Should().Be(3);
        result.Span[0].ToString().Should().Be("aaa");
        result.Span[1].ToString().Should().Be("bbb");
        result.Span[2].ToString().Should().Be("ccc");
    }

    [Fact]
    public void lease_redis_value_processor_empty_array()
    {
        // Empty array (key doesn't exist)
        var resp = "*0\r\n";
        var processor = ResultProcessor.LeaseRedisValue;
        using var result = Execute(resp, processor);

        Assert.NotNull(result);
        result.Length.Should().Be(0);
    }

    [Theory]
    [InlineData("*-1\r\n")] // null array (RESP2)
    [InlineData("_\r\n")] // null (RESP3)
    public void lease_redis_value_processor_null_array(string resp)
    {
        //Arrange
        var processor = ResultProcessor.LeaseRedisValue;

        //Act
        var result = Execute(resp, processor);

        //Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("$5\r\nhello\r\n")] // scalar string (not an array)
    [InlineData(":42\r\n")] // scalar integer (not an array)
    [InlineData("+OK\r\n")] // simple string (not an array)
    public void lease_redis_value_processor_invalid_input(string resp)
    {
        var processor = ResultProcessor.LeaseRedisValue;
        ExecuteUnexpected(resp, processor);
    }

    [Fact]
    public void lease_redis_value_processor_mixed_types()
    {
        // Array with mixed types: bulk string, simple string, integer
        var resp = "*3\r\n$5\r\nhello\r\n+world\r\n:42\r\n";
        var processor = ResultProcessor.LeaseRedisValue;
        using var result = Execute(resp, processor);

        Assert.NotNull(result);
        result.Length.Should().Be(3);
        result.Span[0].ToString().Should().Be("hello");
        result.Span[1].ToString().Should().Be("world");
        result.Span[2].ToString().Should().Be("42");
    }
}
