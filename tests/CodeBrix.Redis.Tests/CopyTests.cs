using System;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class CopyTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    [Fact]
    public async Task basic()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);
        var db = conn.GetDatabase();
        var src = Me();
        var dest = Me() + "2";
        _ = db.KeyDelete(dest);
        _ = db.StringSetAsync(src, "Heyyyyy");
        var ke1 = db.KeyCopyAsync(src, dest).ForAwait();

        //Act
        var ku1 = db.StringGet(dest);

        //Assert
        (await ke1).Should().BeTrue();
        ku1.Equals("Heyyyyy").Should().BeTrue();
    }

    [Fact]
    public async Task cross_db()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var dbDestId = TestConfig.GetDedicatedDB(conn);
        var dbDest = conn.GetDatabase(dbDestId);

        var src = Me();
        var dest = Me() + "2";
        dbDest.KeyDelete(dest);

        _ = db.StringSetAsync(src, "Heyyyyy");
        var ke1 = db.KeyCopyAsync(src, dest, dbDestId).ForAwait();
        var ku1 = dbDest.StringGet(dest);
        (await ke1).Should().BeTrue();
        ku1.Equals("Heyyyyy").Should().BeTrue();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => db.KeyCopyAsync(src, dest, destinationDatabase: -10));
    }

    [Fact]
    public async Task with_replace()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var src = Me();
        var dest = Me() + "2";
        _ = db.StringSetAsync(src, "foo1");
        _ = db.StringSetAsync(dest, "foo2");
        var ke1 = db.KeyCopyAsync(src, dest).ForAwait();
        var ke2 = db.KeyCopyAsync(src, dest, replace: true).ForAwait();
        var ku1 = db.StringGet(dest);
        (await ke1).Should().BeFalse(); // Should fail when not using replace and destination key exist
        (await ke2).Should().BeTrue();
        ku1.Equals("foo1").Should().BeTrue();
    }
}
