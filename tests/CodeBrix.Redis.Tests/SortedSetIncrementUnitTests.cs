using System;
using System.Collections.Generic;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class SortedSetIncrementUnitTests
{
    [Theory]
    [MemberData(nameof(InvalidValueConditions))]
    public void invalid_value_condition_modes_throw(ValueCondition condition)
    {
        var db = new RedisDatabase(null!, 0, null);

        Assert.Throws<InvalidOperationException>(() =>
            db.SortedSetIncrement("key", "member", 1, condition, CommandFlags.None));

        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = db.SortedSetIncrementAsync("key", "member", 1, condition, CommandFlags.None);
        });
    }

    public static IEnumerable<object[]> InvalidValueConditions()
    {
        yield return [ValueCondition.Equal("value")];
        yield return [ValueCondition.NotEqual("value")];
        yield return [ValueCondition.DigestEqual("value")];
        yield return [ValueCondition.DigestNotEqual("value")];
    }
}
