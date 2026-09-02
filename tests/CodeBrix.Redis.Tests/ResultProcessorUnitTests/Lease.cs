using System;
using System.Text;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

/// <summary>
/// Tests for Lease result processors.
/// </summary>
public class Lease(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    [Theory]
    [InlineData("*0\r\n", 0)] // empty array
    [InlineData("*3\r\n,1.5\r\n,2.5\r\n,3.5\r\n", 3)] // 3 floats
    [InlineData("*2\r\n:1\r\n:2\r\n", 2)] // integers converted to floats
    [InlineData("*1\r\n$3\r\n1.5\r\n", 1)] // bulk string converted to float
    [InlineData("*?\r\n,1.5\r\n,2.5\r\n,3.5\r\n.\r\n", 3)] // streaming aggregate with 3 floats
    [InlineData("*?\r\n.\r\n", 0)] // streaming empty array
    public void lease_float32_processor_valid_input(string resp, int expectedCount)
    {
        //Arrange
        var processor = ResultProcessor.LeaseFloat32;

        //Act
        using var result = Execute(resp, processor);

        //Assert
        Assert.NotNull(result);
        result.Length.Should().Be(expectedCount);
    }

    [Fact]
    public void lease_float32_processor_validates_content()
    {
        // Array of 3 floats: 1.5, 2.5, 3.5
        var resp = "*3\r\n,1.5\r\n,2.5\r\n,3.5\r\n";
        var processor = ResultProcessor.LeaseFloat32;
        using var result = Execute(resp, processor);

        Assert.NotNull(result);
        result.Length.Should().Be(3);
        result.Span[0].Should().Be(1.5f);
        result.Span[1].Should().Be(2.5f);
        result.Span[2].Should().Be(3.5f);
    }

    [Theory]
    [InlineData("*-1\r\n")] // null array (RESP2)
    [InlineData("_\r\n")] // null (RESP3)
    public void lease_float32_processor_null_array(string resp)
    {
        //Arrange
        var processor = ResultProcessor.LeaseFloat32;

        //Act
        var result = Execute(resp, processor);

        //Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("$5\r\nhello\r\n")] // scalar string (not an array)
    [InlineData(":42\r\n")] // scalar integer (not an array)
    public void lease_float32_processor_invalid_input(string resp)
    {
        var processor = ResultProcessor.LeaseFloat32;
        ExecuteUnexpected(resp, processor);
    }

    [Theory]
    [InlineData("$5\r\nhello\r\n", "hello")] // bulk string
    [InlineData("+world\r\n", "world")] // simple string
    [InlineData(":42\r\n", "42")] // integer
    public void lease_processor_valid_input(string resp, string expected)
    {
        var processor = ResultProcessor.Lease;
        using var result = Execute(resp, processor);

        Assert.NotNull(result);
        var str = Encoding.UTF8.GetString(result.Span);
        str.Should().Be(expected);
    }

    [Theory]
    [InlineData("*1\r\n$5\r\nhello\r\n", "hello")] // array of 1 bulk string
    [InlineData("*1\r\n+world\r\n", "world")] // array of 1 simple string
    public void lease_from_array_processor_valid_input(string resp, string expected)
    {
        var processor = ResultProcessor.LeaseFromArray;
        using var result = Execute(resp, processor);

        Assert.NotNull(result);
        var str = Encoding.UTF8.GetString(result.Span);
        str.Should().Be(expected);
    }

    [Theory]
    [InlineData("*0\r\n")] // empty array
    [InlineData("*2\r\n$5\r\nhello\r\n$5\r\nworld\r\n")] // array of 2 (not 1)
    public void lease_from_array_processor_invalid_input(string resp)
    {
        var processor = ResultProcessor.LeaseFromArray;
        ExecuteUnexpected(resp, processor);
    }
}
