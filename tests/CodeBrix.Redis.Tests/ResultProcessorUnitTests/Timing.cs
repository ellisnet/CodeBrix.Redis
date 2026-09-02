using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

public class Timing(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    [Theory]
    [InlineData("+OK\r\n")]
    [InlineData(":42\r\n")]
    [InlineData(":0\r\n")]
    [InlineData(":-1\r\n")]
    [InlineData("$5\r\nhello\r\n")]
    [InlineData("$0\r\n\r\n")]
    [InlineData("$-1\r\n")]
    [InlineData("*2\r\n:1\r\n:2\r\n")]
    [InlineData("*0\r\n")]
    [InlineData("_\r\n")]
    public void timing_valid_response_returns_time_span(string resp)
    {
        //Arrange
        var processor = ResultProcessor.ResponseTimer;
        var message = ResultProcessor.TimingProcessor.CreateMessage(-1, CommandFlags.None, RedisCommand.PING);

        //Act
        var result = Execute(resp, processor, message);

        //Assert
        result.Should().NotBe(System.TimeSpan.MaxValue);
        (result >= System.TimeSpan.Zero).Should().BeTrue();
    }
}
