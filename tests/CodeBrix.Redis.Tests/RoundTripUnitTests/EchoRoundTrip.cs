using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.RoundTripUnitTests; //was previously: StackExchange.Redis.Tests.RoundTripUnitTests;

public class EchoRoundTrip
{
    [Theory(Timeout = 5000)]
    [InlineData("hello", "*2\r\n$4\r\nECHO\r\n$5\r\nhello\r\n", "+hello\r\n")]
    [InlineData("hello", "*2\r\n$4\r\nECHO\r\n$5\r\nhello\r\n", "$5\r\nhello\r\n")]
    public async Task echo_round_trip_test(string payload, string requestResp, string responseResp)
    {
        var msg = Message.Create(-1, CommandFlags.None, RedisCommand.ECHO, (RedisValue)payload);
        var result = await TestConnection.ExecuteAsync(msg, ResultProcessor.String, requestResp, responseResp, cancellationToken: TestContext.Current.CancellationToken);
        result.Should().Be(payload);
    }
}
