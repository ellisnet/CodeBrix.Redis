using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.Issues; //was previously: StackExchange.Redis.Tests.Issues;

public class Issue2176Tests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task execute_batch()
    {
        await using var conn = Create();
        var db = conn.GetDatabase();

        var me = Me();
        var key = me + ":1";
        var key2 = me + ":2";
        var keyIntersect = me + ":result";

        db.KeyDelete(key);
        db.KeyDelete(key2);
        db.KeyDelete(keyIntersect);
        db.SortedSetAdd(key, "a", 1345);

        var tasks = new List<Task>();
        var batch = db.CreateBatch();
        tasks.Add(batch.SortedSetAddAsync(key2, "a", 4567));
        tasks.Add(batch.SortedSetCombineAndStoreAsync(SetOperation.Intersect, keyIntersect, [key, key2]));
        var rangeByRankTask = batch.SortedSetRangeByRankAsync(keyIntersect);
        tasks.Add(rangeByRankTask);
        batch.Execute();

        await Task.WhenAll(tasks.ToArray());

        var rangeByRankSortedSetValues = rangeByRankTask.Result;

        int size = rangeByRankSortedSetValues.Length;
        size.Should().Be(1);
        string firstRedisValue = rangeByRankSortedSetValues.FirstOrDefault().ToString();
        firstRedisValue.Should().Be("a");
    }

    [Fact]
    public async Task execute_transaction()
    {
        await using var conn = Create();
        var db = conn.GetDatabase();

        var me = Me();
        var key = me + ":1";
        var key2 = me + ":2";
        var keyIntersect = me + ":result";

        db.KeyDelete(key);
        db.KeyDelete(key2);
        db.KeyDelete(keyIntersect);
        db.SortedSetAdd(key, "a", 1345);

        var tasks = new List<Task>();
        var batch = db.CreateTransaction();
        tasks.Add(batch.SortedSetAddAsync(key2, "a", 4567));
        tasks.Add(batch.SortedSetCombineAndStoreAsync(SetOperation.Intersect, keyIntersect, [key, key2]));
        var rangeByRankTask = batch.SortedSetRangeByRankAsync(keyIntersect);
        tasks.Add(rangeByRankTask);
        batch.Execute();

        await Task.WhenAll(tasks.ToArray());

        var rangeByRankSortedSetValues = rangeByRankTask.Result;

        int size = rangeByRankSortedSetValues.Length;
        size.Should().Be(1);
        string firstRedisValue = rangeByRankSortedSetValues.FirstOrDefault().ToString();
        firstRedisValue.Should().Be("a");
    }
}
