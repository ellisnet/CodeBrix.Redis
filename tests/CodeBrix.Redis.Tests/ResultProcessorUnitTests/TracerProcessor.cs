using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

public class TracerProcessor(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    [Fact]
    public void ping_pong_success()
    {
        // PING response - simple string
        var resp = "+PONG\r\n";
        var message = Message.Create(-1, default, RedisCommand.PING);

        var result = Execute(resp, ResultProcessor.Tracer, message);

        result.Should().BeTrue();
    }

    [Fact]
    public void time_success()
    {
        // TIME response - array of 2 elements
        var resp = "*2\r\n$10\r\n1609459200\r\n$6\r\n123456\r\n";
        var message = Message.Create(-1, default, RedisCommand.TIME);

        var result = Execute(resp, ResultProcessor.Tracer, message);

        result.Should().BeTrue();
    }
}
