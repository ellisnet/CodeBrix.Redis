using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

public class RedisValueFromArray(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    [Fact]
    public void single_element_array_string()
    {
        //Act
        var result = Execute("*1\r\n$5\r\nhello\r\n", ResultProcessor.RedisValueFromArray);

        //Assert
        ((string?)result).Should().Be("hello");
    }

    [Fact]
    public void single_element_array_integer()
    {
        //Act
        var result = Execute("*1\r\n:42\r\n", ResultProcessor.RedisValueFromArray);

        //Assert
        ((long)result).Should().Be(42);
    }

    [Fact]
    public void single_element_array_null()
    {
        //Act
        var result = Execute("*1\r\n$-1\r\n", ResultProcessor.RedisValueFromArray);

        //Assert
        result.IsNull.Should().BeTrue();
    }

    [Fact]
    public void single_element_array_empty_string()
    {
        //Act
        var result = Execute("*1\r\n$0\r\n\r\n", ResultProcessor.RedisValueFromArray);

        //Assert
        ((string?)result).Should().Be("");
    }
}
