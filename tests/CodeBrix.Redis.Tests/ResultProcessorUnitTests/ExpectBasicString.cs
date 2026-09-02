using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

public class ExpectBasicString(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    [Theory]
    [InlineData("+OK\r\n", true)]
    [InlineData("$2\r\nOK\r\n", true)]
    public void demand_ok_success(string resp, bool expected) => Execute(resp, ResultProcessor.DemandOK).Should().Be(expected);

    [Theory]
    [InlineData("+PONG\r\n", true)]
    [InlineData("$4\r\nPONG\r\n", true)]
    public void demand_pong_success(string resp, bool expected) => Execute(resp, ResultProcessor.DemandPONG).Should().Be(expected);

    [Theory]
    [InlineData("+FAIL\r\n")]
    [InlineData("$4\r\nFAIL\r\n")]
    [InlineData(":1\r\n")]
    public void demand_ok_failure(string resp) => TryExecute(resp, ResultProcessor.DemandOK, out _, out _).Should().BeFalse();

    [Theory]
    [InlineData("+FAIL\r\n")]
    [InlineData("$4\r\nFAIL\r\n")]
    [InlineData(":1\r\n")]
    public void demand_pong_failure(string resp) => TryExecute(resp, ResultProcessor.DemandPONG, out _, out _).Should().BeFalse();

    [Theory]
    [InlineData("+Background saving started\r\n", true)]
    [InlineData("$25\r\nBackground saving started\r\n", true)]
    [InlineData("+Background saving started by parent\r\n", true)]
    public void background_save_started_success(string resp, bool expected) => Execute(resp, ResultProcessor.BackgroundSaveStarted).Should().Be(expected);

    [Theory]
    [InlineData("+Background append only file rewriting started\r\n", true)]
    [InlineData("$45\r\nBackground append only file rewriting started\r\n", true)]
    public void background_save_aof_started_success(string resp, bool expected) => Execute(resp, ResultProcessor.BackgroundSaveAOFStarted).Should().Be(expected);

    // Case sensitivity tests - these demonstrate that the new implementation is case-sensitive
    // The old CommandBytes implementation was case-insensitive (stored uppercase)
    [Theory]
    [InlineData("+ok\r\n")] // lowercase
    [InlineData("+Ok\r\n")] // mixed case
    [InlineData("$2\r\nok\r\n")] // lowercase bulk string
    public void demand_ok_case_sensitive_failure(string resp) => TryExecute(resp, ResultProcessor.DemandOK, out _, out _).Should().BeFalse();

    [Theory]
    [InlineData("+pong\r\n")] // lowercase
    [InlineData("+Pong\r\n")] // mixed case
    [InlineData("$4\r\npong\r\n")] // lowercase bulk string
    public void demand_pong_case_sensitive_failure(string resp) => TryExecute(resp, ResultProcessor.DemandPONG, out _, out _).Should().BeFalse();

    [Theory]
    [InlineData("+background saving started\r\n")] // lowercase
    [InlineData("+BACKGROUND SAVING STARTED\r\n")] // uppercase
    public void background_save_started_case_sensitive_failure(string resp) => TryExecute(resp, ResultProcessor.BackgroundSaveStarted, out _, out _).Should().BeFalse();
}
