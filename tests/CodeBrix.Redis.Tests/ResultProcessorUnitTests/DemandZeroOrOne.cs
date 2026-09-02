using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

public class DemandZeroOrOne(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    [Theory]
    [InlineData(":0\r\n", false)]
    [InlineData(":1\r\n", true)]
    [InlineData("+0\r\n", false)]
    [InlineData("+1\r\n", true)]
    [InlineData("$1\r\n0\r\n", false)]
    [InlineData("$1\r\n1\r\n", true)]
    public void valid_zero_or_one_success(string resp, bool expected)
    {
        //Act
        var result = Execute(resp, ResultProcessor.DemandZeroOrOne);

        //Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(":2\r\n")]
    [InlineData("+OK\r\n")]
    [InlineData("*1\r\n:1\r\n")]
    [InlineData("$-1\r\n")]
    public void invalid_response_failure(string resp) => ExecuteUnexpected(resp, ResultProcessor.DemandZeroOrOne);
}
