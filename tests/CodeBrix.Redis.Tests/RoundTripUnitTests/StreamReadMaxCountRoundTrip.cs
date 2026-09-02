using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.RoundTripUnitTests; //was previously: StackExchange.Redis.Tests.RoundTripUnitTests;

public class StreamReadMaxCountRoundTrip(ITestOutputHelper log)
{
    [Fact(Timeout = 1000)]
    public async Task x_read_count_max_count_max_size_ordered_after_count()
    {
        StreamPosition[] positions = [new StreamPosition("sa", "5-5")];
        var msg = new RedisDatabase.MultiStreamReadCommandMessage(0, CommandFlags.None, positions, countPerStream: 2, maxCount: 3, maxSize: 100);

        // COUNT, then MAXCOUNT, then MAXSIZE, then STREAMS
        const string requestResp =
            "*10\r\n$5\r\nXREAD\r\n$5\r\nCOUNT\r\n$1\r\n2\r\n$8\r\nMAXCOUNT\r\n$1\r\n3\r\n$7\r\nMAXSIZE\r\n$3\r\n100\r\n$7\r\nSTREAMS\r\n$2\r\nsa\r\n$3\r\n5-5\r\n";

        var result = await TestConnection.ExecuteAsync(msg, ResultProcessor.MultiStream, requestResp, "*-1\r\n", log: log, cancellationToken: TestContext.Current.CancellationToken);
        result.Should().BeEmpty();
    }

    [Fact(Timeout = 1000)]
    public async Task x_read_group_count_max_count_max_size_ordered_after_count()
    {
        StreamPosition[] positions = [new StreamPosition("sa", "5-5")];
        var msg = new RedisDatabase.MultiStreamReadGroupCommandMessage(0, CommandFlags.None, positions, "g", "c", countPerStream: 2, noAck: false, claimMinIdleTime: null, maxCount: 3, maxSize: 100);

        const string requestResp =
            "*13\r\n$10\r\nXREADGROUP\r\n$5\r\nGROUP\r\n$1\r\ng\r\n$1\r\nc\r\n$5\r\nCOUNT\r\n$1\r\n2\r\n$8\r\nMAXCOUNT\r\n$1\r\n3\r\n$7\r\nMAXSIZE\r\n$3\r\n100\r\n$7\r\nSTREAMS\r\n$2\r\nsa\r\n$3\r\n5-5\r\n";

        var result = await TestConnection.ExecuteAsync(msg, ResultProcessor.MultiStream, requestResp, "*-1\r\n", log: log, cancellationToken: TestContext.Current.CancellationToken);
        result.Should().BeEmpty();
    }
}
