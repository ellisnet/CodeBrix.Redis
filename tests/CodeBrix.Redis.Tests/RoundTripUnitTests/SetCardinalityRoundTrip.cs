using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.RoundTripUnitTests; //was previously: StackExchange.Redis.Tests.RoundTripUnitTests;

public class SetCardinalityRoundTrip(ITestOutputHelper log)
{
    [Fact(Timeout = 1000)]
    public async Task s_diff_card_no_limit_round_trips()
    {
        var msg = new SetOperationCardinalityMessage(0, CommandFlags.None, RedisCommand.SDIFFCARD, ["s1", "s2"], 0, approximate: false);
        const string requestResp = "*4\r\n$9\r\nSDIFFCARD\r\n$1\r\n2\r\n$2\r\ns1\r\n$2\r\ns2\r\n";

        var result = await TestConnection.ExecuteAsync(msg, ResultProcessor.Int64, requestResp, ":2\r\n", log: log, cancellationToken: TestContext.Current.CancellationToken);
        result.Should().Be(2);
    }

    [Fact(Timeout = 1000)]
    public async Task s_union_card_with_limit_round_trips()
    {
        var msg = new SetOperationCardinalityMessage(0, CommandFlags.None, RedisCommand.SUNIONCARD, ["s1", "s2"], 3, approximate: false);
        const string requestResp = "*6\r\n$10\r\nSUNIONCARD\r\n$1\r\n2\r\n$2\r\ns1\r\n$2\r\ns2\r\n$5\r\nLIMIT\r\n$1\r\n3\r\n";

        var result = await TestConnection.ExecuteAsync(msg, ResultProcessor.Int64, requestResp, ":3\r\n", log: log, cancellationToken: TestContext.Current.CancellationToken);
        result.Should().Be(3);
    }

    [Fact(Timeout = 1000)]
    public async Task s_union_card_approx_with_limit_round_trips()
    {
        // APPROX is written before LIMIT
        var msg = new SetOperationCardinalityMessage(0, CommandFlags.None, RedisCommand.SUNIONCARD, ["s1", "s2"], 3, approximate: true);
        const string requestResp = "*7\r\n$10\r\nSUNIONCARD\r\n$1\r\n2\r\n$2\r\ns1\r\n$2\r\ns2\r\n$6\r\nAPPROX\r\n$5\r\nLIMIT\r\n$1\r\n3\r\n";

        var result = await TestConnection.ExecuteAsync(msg, ResultProcessor.Int64, requestResp, ":3\r\n", log: log, cancellationToken: TestContext.Current.CancellationToken);
        result.Should().Be(3);
    }
}
