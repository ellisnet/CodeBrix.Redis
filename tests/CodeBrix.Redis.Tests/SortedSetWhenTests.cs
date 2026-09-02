using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class SortedSetWhenTest(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    [Fact]
    public async Task greater_than_less_than()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var key = Me();
        var member = "a";
        db.KeyDelete(key, CommandFlags.FireAndForget);

        //Act
        db.SortedSetAdd(key, member, 2);

        //Assert
        db.SortedSetUpdate(key, member, 5, when: SortedSetWhen.GreaterThan).Should().BeTrue();
        db.SortedSetUpdate(key, member, 1, when: SortedSetWhen.GreaterThan).Should().BeFalse();
        db.SortedSetUpdate(key, member, 1, when: SortedSetWhen.LessThan).Should().BeTrue();
        db.SortedSetUpdate(key, member, 5, when: SortedSetWhen.LessThan).Should().BeFalse();
    }

    [Fact]
    public async Task increment()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var key = Me();
        var member = "a";
        var missingMember = "b";
        db.KeyDelete(key, CommandFlags.FireAndForget);

        //Act
        db.SortedSetAdd(key, member, 2);

        //Assert
        db.SortedSetIncrement(key, member, 3, ValueCondition.Always, CommandFlags.None).Should().Be(5);
        db.SortedSetIncrement(key, member, 1, ValueCondition.Exists, CommandFlags.None).Should().Be(6);
        db.SortedSetIncrement(key, missingMember, 1, ValueCondition.Exists, CommandFlags.None).Should().BeNull();
        db.SortedSetIncrement(key, missingMember, 1, ValueCondition.NotExists, CommandFlags.None).Should().Be(1);
        db.SortedSetIncrement(key, member, 1, ValueCondition.NotExists, CommandFlags.None).Should().BeNull();
        (await db.SortedSetIncrementAsync(key, member, 2, ValueCondition.Exists, CommandFlags.None)).Should().Be(8);
    }

    [Fact]
    public async Task illegal_combinations()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var key = Me();
        var member = "a";

        //Act
        db.KeyDelete(key, CommandFlags.FireAndForget);

        //Assert
        Assert.Throws<CodeBrix.Redis.RedisServerException>(() => db.SortedSetAdd(key, member, 5, when: SortedSetWhen.LessThan | SortedSetWhen.GreaterThan));
        Assert.Throws<CodeBrix.Redis.RedisServerException>(() => db.SortedSetAdd(key, member, 5, when: SortedSetWhen.Exists | SortedSetWhen.NotExists));
        Assert.Throws<CodeBrix.Redis.RedisServerException>(() => db.SortedSetAdd(key, member, 5, when: SortedSetWhen.GreaterThan | SortedSetWhen.NotExists));
        Assert.Throws<CodeBrix.Redis.RedisServerException>(() => db.SortedSetAdd(key, member, 5, when: SortedSetWhen.LessThan | SortedSetWhen.NotExists));
    }
}
