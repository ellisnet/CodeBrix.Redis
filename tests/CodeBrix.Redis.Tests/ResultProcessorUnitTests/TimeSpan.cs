using System;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

/// <summary>
/// Tests for TimeSpanProcessor
/// </summary>
public class TimeSpanTests(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    [Theory]
    [InlineData(":0\r\n", 0)]
    [InlineData(":1\r\n", 1)]
    [InlineData(":1000\r\n", 1000)]
    [InlineData(":60\r\n", 60)]
    [InlineData(":3600\r\n", 3600)]
    public void time_span_from_seconds_valid_integer(string resp, long seconds)
    {
        //Arrange
        var processor = ResultProcessor.TimeSpanFromSeconds;

        //Act
        var result = Execute(resp, processor);

        //Assert
        Assert.NotNull(result);
        result.Value.Should().Be(TimeSpan.FromSeconds(seconds));
    }

    [Theory]
    [InlineData(":0\r\n", 0)]
    [InlineData(":1\r\n", 1)]
    [InlineData(":1000\r\n", 1000)]
    [InlineData(":60000\r\n", 60000)]
    [InlineData(":3600000\r\n", 3600000)]
    public void time_span_from_milliseconds_valid_integer(string resp, long milliseconds)
    {
        //Arrange
        var processor = ResultProcessor.TimeSpanFromMilliseconds;

        //Act
        var result = Execute(resp, processor);

        //Assert
        Assert.NotNull(result);
        result.Value.Should().Be(TimeSpan.FromMilliseconds(milliseconds));
    }

    [Theory]
    [InlineData(":-1\r\n")]
    [InlineData(":-2\r\n")]
    [InlineData(":-100\r\n")]
    public void time_span_from_seconds_negative_integer_returns_null(string resp)
    {
        //Arrange
        var processor = ResultProcessor.TimeSpanFromSeconds;

        //Act
        var result = Execute(resp, processor);

        //Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(":-1\r\n")]
    [InlineData(":-2\r\n")]
    [InlineData(":-100\r\n")]
    public void time_span_from_milliseconds_negative_integer_returns_null(string resp)
    {
        //Arrange
        var processor = ResultProcessor.TimeSpanFromMilliseconds;

        //Act
        var result = Execute(resp, processor);

        //Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("$-1\r\n")] // RESP2 null bulk string
    [InlineData("_\r\n")] // RESP3 null
    public void time_span_from_seconds_null_returns_null(string resp)
    {
        //Arrange
        var processor = ResultProcessor.TimeSpanFromSeconds;

        //Act
        var result = Execute(resp, processor);

        //Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("$-1\r\n")] // RESP2 null bulk string
    [InlineData("_\r\n")] // RESP3 null
    public void time_span_from_milliseconds_null_returns_null(string resp)
    {
        //Arrange
        var processor = ResultProcessor.TimeSpanFromMilliseconds;

        //Act
        var result = Execute(resp, processor);

        //Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("+OK\r\n")]
    [InlineData("$2\r\nOK\r\n")]
    [InlineData("*2\r\n:1\r\n:2\r\n")]
    public void time_span_from_seconds_invalid_type(string resp)
    {
        var processor = ResultProcessor.TimeSpanFromSeconds;
        ExecuteUnexpected(resp, processor);
    }

    [Theory]
    [InlineData("+OK\r\n")]
    [InlineData("$2\r\nOK\r\n")]
    [InlineData("*2\r\n:1\r\n:2\r\n")]
    public void time_span_from_milliseconds_invalid_type(string resp)
    {
        var processor = ResultProcessor.TimeSpanFromMilliseconds;
        ExecuteUnexpected(resp, processor);
    }
}
