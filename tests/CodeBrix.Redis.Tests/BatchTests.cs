using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class BatchTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    [Fact]
    public async Task test_batch_not_sent()
    {
        //Arrange
        await using var conn = Create();
        var db = conn.GetDatabase();
        var key = Me();
        _ = db.KeyDeleteAsync(key);
        _ = db.StringSetAsync(key, "batch-not-sent");
        var batch = db.CreateBatch();
        _ = batch.KeyDeleteAsync(key);
        _ = batch.SetAddAsync(key, "a");
        _ = batch.SetAddAsync(key, "b");

        //Act
        _ = batch.SetAddAsync(key, "c");

        //Assert
        db.StringGet(key).Should().Be("batch-not-sent");
    }

    [Fact]
    public async Task test_batch_sent()
    {
        //Arrange
        await using var conn = Create();
        var db = conn.GetDatabase();
        var key = Me();
        _ = db.KeyDeleteAsync(key);
        _ = db.StringSetAsync(key, "batch-sent");
        var tasks = new List<Task>();
        var batch = db.CreateBatch();
        tasks.Add(batch.KeyDeleteAsync(key));
        tasks.Add(batch.SetAddAsync(key, "a"));
        tasks.Add(batch.SetAddAsync(key, "b"));
        tasks.Add(batch.SetAddAsync(key, "c"));
        batch.Execute();
        var result = db.SetMembersAsync(key);
        tasks.Add(result);
        await Task.WhenAll(tasks.ToArray());
        var arr = result.Result;

        //Act
        Array.Sort(arr, (x, y) => string.Compare(x, y));

        //Assert
        arr.Length.Should().Be(3);
        arr[0].Should().Be("a");
        arr[1].Should().Be("b");
        arr[2].Should().Be("c");
    }
}
