using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.Issues; //was previously: StackExchange.Redis.Tests.Issues;

public class SO23949477Tests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task execute()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.SortedSetAdd(key, "c", 3, When.Always, CommandFlags.FireAndForget);
        db.SortedSetAdd(
            key,
            [
                new SortedSetEntry("a", 1),
                new SortedSetEntry("b", 2),
                new SortedSetEntry("d", 4),
                new SortedSetEntry("e", 5),
            ],
            When.Always,
            CommandFlags.FireAndForget);
        var pairs = db.SortedSetRangeByScoreWithScores(
            key, order: Order.Descending, take: 3);
        pairs.Length.Should().Be(3);
        pairs[0].Score.Should().Be(5);
        pairs[0].Element.Should().Be("e");
        pairs[1].Score.Should().Be(4);
        pairs[1].Element.Should().Be("d");
        pairs[2].Score.Should().Be(3);
        pairs[2].Element.Should().Be("c");
    }
}
