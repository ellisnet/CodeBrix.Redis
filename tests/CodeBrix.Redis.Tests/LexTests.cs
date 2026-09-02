using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class LexTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    [Fact]
    public async Task query_range_and_length_by_lex()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        db.SortedSetAdd(
            key,
            [
                    new SortedSetEntry("a", 0),
                    new SortedSetEntry("b", 0),
                    new SortedSetEntry("c", 0),
                    new SortedSetEntry("d", 0),
                    new SortedSetEntry("e", 0),
                    new SortedSetEntry("f", 0),
                    new SortedSetEntry("g", 0),
            ],
            CommandFlags.FireAndForget);

        var set = db.SortedSetRangeByValue(key, default(RedisValue), "c");
        var count = db.SortedSetLengthByValue(key, default(RedisValue), "c");
        Equate(set, count, "a", "b", "c");

        set = db.SortedSetRangeByValue(key, default(RedisValue), "c", Exclude.Stop);
        count = db.SortedSetLengthByValue(key, default(RedisValue), "c", Exclude.Stop);
        Equate(set, count, "a", "b");

        set = db.SortedSetRangeByValue(key, "aaa", "g", Exclude.Stop);
        count = db.SortedSetLengthByValue(key, "aaa", "g", Exclude.Stop);
        Equate(set, count, "b", "c", "d", "e", "f");

        set = db.SortedSetRangeByValue(key, "aaa", "g", Exclude.Stop, 1, 3);
        Equate(set, set.Length, "c", "d", "e");

        set = db.SortedSetRangeByValue(key, "aaa", "g", Exclude.Stop, Order.Descending, 1, 3);
        Equate(set, set.Length, "e", "d", "c");

        set = db.SortedSetRangeByValue(key, "g", "aaa", Exclude.Start, Order.Descending, 1, 3);
        Equate(set, set.Length, "e", "d", "c");

        set = db.SortedSetRangeByValue(key, "e", default(RedisValue));
        count = db.SortedSetLengthByValue(key, "e", default(RedisValue));
        Equate(set, count, "e", "f", "g");

        set = db.SortedSetRangeByValue(key, RedisValue.Null, RedisValue.Null, Exclude.None, Order.Descending, 0, 3);    // added to test Null-min- and max-param
        Equate(set, set.Length, "g", "f", "e");
    }

    [Fact]
    public async Task remove_range_by_lex()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        db.SortedSetAdd(
            key,
            [
                    new SortedSetEntry("aaaa", 0),
                    new SortedSetEntry("b", 0),
                    new SortedSetEntry("c", 0),
                    new SortedSetEntry("d", 0),
                    new SortedSetEntry("e", 0),
            ],
            CommandFlags.FireAndForget);
        db.SortedSetAdd(
            key,
            [
                    new SortedSetEntry("foo", 0),
                    new SortedSetEntry("zap", 0),
                    new SortedSetEntry("zip", 0),
                    new SortedSetEntry("ALPHA", 0),
                    new SortedSetEntry("alpha", 0),
            ],
            CommandFlags.FireAndForget);

        var set = db.SortedSetRangeByRank(key);
        Equate(set, set.Length, "ALPHA", "aaaa", "alpha", "b", "c", "d", "e", "foo", "zap", "zip");

        long removed = db.SortedSetRemoveRangeByValue(key, "alpha", "omega");
        removed.Should().Be(6);

        set = db.SortedSetRangeByRank(key);
        Equate(set, set.Length, "ALPHA", "aaaa", "zap", "zip");
    }

    private static void Equate(RedisValue[] actual, long count, params string[] expected)
    {
        count.Should().Be(expected.Length);
        actual.Length.Should().Be(expected.Length);
        for (int i = 0; i < actual.Length; i++)
        {
            actual[i].Should().Be(expected[i]);
        }
    }
}
