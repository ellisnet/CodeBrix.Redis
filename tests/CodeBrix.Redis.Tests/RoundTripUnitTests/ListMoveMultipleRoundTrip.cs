using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.RoundTripUnitTests; //was previously: StackExchange.Redis.Tests.RoundTripUnitTests;

public class ListMoveMultipleRoundTrip(ITestOutputHelper log)
{
    // builds the message exactly as RedisDatabase.ListMove(... count ...) does, and asserts the wire bytes.
    private static Message CreateMessage(ListSide from, ListSide to, long count, ListMoveCount mode, ListMoveOrder order) =>
        Message.Create(
            0,
            CommandFlags.None,
            RedisCommand.LMOVEM,
            (RedisKey)"s",
            (RedisKey)"d",
            from.ToLiteral(),
            to.ToLiteral(),
            mode.ToLiteral(),
            count,
            order.ToLiteral());

    [Fact(Timeout = 1000)]
    public async Task up_to_bulk_round_trips()
    {
        var msg = CreateMessage(ListSide.Left, ListSide.Right, 2, ListMoveCount.UpTo, ListMoveOrder.Bulk);
        const string requestResp =
            "*8\r\n$6\r\nLMOVEM\r\n$1\r\ns\r\n$1\r\nd\r\n$4\r\nLEFT\r\n$5\r\nRIGHT\r\n$5\r\nCOUNT\r\n$1\r\n2\r\n$4\r\nBULK\r\n";

        var result = await TestConnection.ExecuteAsync(
            msg, ResultProcessor.NullableRedisValueArray, requestResp, "*2\r\n$1\r\na\r\n$1\r\nb\r\n", log: log,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result); //kept as the xUnit form: it carries [NotNull], so the compiler's null-state flows to the dereference below
        result.Length.Should().Be(2);
        result[0].ToString().Should().Be("a");
        result[1].ToString().Should().Be("b");
    }

    [Fact(Timeout = 1000)]
    public async Task exactly_one_by_one_not_satisfied_round_trips_null()
    {
        var msg = CreateMessage(ListSide.Right, ListSide.Left, 3, ListMoveCount.Exactly, ListMoveOrder.OneByOne);
        const string requestResp =
            "*8\r\n$6\r\nLMOVEM\r\n$1\r\ns\r\n$1\r\nd\r\n$5\r\nRIGHT\r\n$4\r\nLEFT\r\n$7\r\nEXACTLY\r\n$1\r\n3\r\n$3\r\nOBO\r\n";

        var result = await TestConnection.ExecuteAsync(
            msg, ResultProcessor.NullableRedisValueArray, requestResp, "*-1\r\n", log: log,
            cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }
}
