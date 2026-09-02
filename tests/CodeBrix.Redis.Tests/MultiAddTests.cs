using System.Linq;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class MultiAddTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    [Fact]
    public async Task add_sorted_set_every_way()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        RedisKey key = Me();

        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.SortedSetAdd(key, "a", 1, CommandFlags.FireAndForget);
        db.SortedSetAdd(
            key,
            [
                new SortedSetEntry("b", 2),
            ],
            CommandFlags.FireAndForget);
        db.SortedSetAdd(
            key,
            [
                new SortedSetEntry("c", 3),
                new SortedSetEntry("d", 4),
            ],
            CommandFlags.FireAndForget);
        db.SortedSetAdd(
            key,
            [
                new SortedSetEntry("e", 5),
                new SortedSetEntry("f", 6),
                new SortedSetEntry("g", 7),
            ],
            CommandFlags.FireAndForget);
        db.SortedSetAdd(
            key,
            [
                new SortedSetEntry("h", 8),
                new SortedSetEntry("i", 9),
                new SortedSetEntry("j", 10),
                new SortedSetEntry("k", 11),
            ],
            CommandFlags.FireAndForget);
        var vals = db.SortedSetRangeByScoreWithScores(key);
        string s = string.Join(",", vals.OrderByDescending(x => x.Score).Select(x => x.Element));
        s.Should().Be("k,j,i,h,g,f,e,d,c,b,a");
        s = string.Join(",", vals.OrderBy(x => x.Score).Select(x => x.Score));
        s.Should().Be("1,2,3,4,5,6,7,8,9,10,11");
    }

    [Fact]
    public async Task add_hash_every_way()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        RedisKey key = Me();

        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.HashSet(key, "a", 1, flags: CommandFlags.FireAndForget);
        db.HashSet(
            key,
            [
                new HashEntry("b", 2),
            ],
            CommandFlags.FireAndForget);
        db.HashSet(
            key,
            [
                new HashEntry("c", 3),
                new HashEntry("d", 4),
            ],
            CommandFlags.FireAndForget);
        db.HashSet(
            key,
            [
                new HashEntry("e", 5),
                new HashEntry("f", 6),
                new HashEntry("g", 7),
            ],
            CommandFlags.FireAndForget);
        db.HashSet(
            key,
            [
                new HashEntry("h", 8),
                new HashEntry("i", 9),
                new HashEntry("j", 10),
                new HashEntry("k", 11),
            ],
            CommandFlags.FireAndForget);
        var vals = db.HashGetAll(key);
        string s = string.Join(",", vals.OrderByDescending(x => (double)x.Value).Select(x => x.Name));
        s.Should().Be("k,j,i,h,g,f,e,d,c,b,a");
        s = string.Join(",", vals.OrderBy(x => (double)x.Value).Select(x => x.Value));
        s.Should().Be("1,2,3,4,5,6,7,8,9,10,11");
    }

    [Fact]
    public async Task add_set_every_way()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        RedisKey key = Me();

        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.SetAdd(key, "a", CommandFlags.FireAndForget);
        db.SetAdd(key, ["b"], CommandFlags.FireAndForget);
        db.SetAdd(key, ["c", "d"], CommandFlags.FireAndForget);
        db.SetAdd(key, ["e", "f", "g"], CommandFlags.FireAndForget);
        db.SetAdd(key, ["h", "i", "j", "k"], CommandFlags.FireAndForget);

        var vals = db.SetMembers(key);
        string s = string.Join(",", vals.OrderByDescending(x => x));
        s.Should().Be("k,j,i,h,g,f,e,d,c,b,a");
    }

    [Fact]
    public async Task add_set_every_way_numbers()
    {
        await using var conn = Create();
        var db = conn.GetDatabase();
        RedisKey key = Me();

        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.SetAdd(key, "a", CommandFlags.FireAndForget);
        db.SetAdd(key, ["1"], CommandFlags.FireAndForget);
        db.SetAdd(key, ["11", "2"], CommandFlags.FireAndForget);
        db.SetAdd(key, ["10", "3", "1.5"], CommandFlags.FireAndForget);
        db.SetAdd(key, ["2.2", "-1", "s", "t"], CommandFlags.FireAndForget);

        var vals = db.SetMembers(key);
        string s = string.Join(",", vals.OrderByDescending(x => x));
        s.Should().Be("t,s,a,11,10,3,2.2,2,1.5,1,-1");
    }
}
