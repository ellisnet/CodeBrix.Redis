using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

/// <summary>
/// Unit tests for Condition subclasses using the RespReader path.
/// </summary>
public class ConditionTests(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    private static Message CreateConditionMessage(Condition condition, RedisCommand command, RedisKey key, params RedisValue[] values)
    {
        return values.Length switch
        {
            0 => Condition.ConditionProcessor.CreateMessage(condition, 0, CommandFlags.None, command, key),
            1 => Condition.ConditionProcessor.CreateMessage(condition, 0, CommandFlags.None, command, key, values[0]),
            2 => Condition.ConditionProcessor.CreateMessage(condition, 0, CommandFlags.None, command, key, values[0], values[1]),
            5 => Condition.ConditionProcessor.CreateMessage(condition, 0, CommandFlags.None, command, key, values[0], values[1], values[2], values[3], values[4]),
            _ => throw new System.NotSupportedException($"Unsupported value count: {values.Length}"),
        };
    }

    [Fact]
    public void exists_condition_key_exists_true()
    {
        //Arrange
        var condition = Condition.KeyExists("mykey");
        var message = CreateConditionMessage(condition, RedisCommand.EXISTS, "mykey");

        //Act
        var result = Execute(":1\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void exists_condition_key_exists_false()
    {
        //Arrange
        var condition = Condition.KeyExists("mykey");
        var message = CreateConditionMessage(condition, RedisCommand.EXISTS, "mykey");

        //Act
        var result = Execute(":0\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void exists_condition_key_not_exists_true()
    {
        //Arrange
        var condition = Condition.KeyNotExists("mykey");
        var message = CreateConditionMessage(condition, RedisCommand.EXISTS, "mykey");

        //Act
        var result = Execute(":0\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void exists_condition_key_not_exists_false()
    {
        //Arrange
        var condition = Condition.KeyNotExists("mykey");
        var message = CreateConditionMessage(condition, RedisCommand.EXISTS, "mykey");

        //Act
        var result = Execute(":1\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void exists_condition_hash_exists_true()
    {
        //Arrange
        var condition = Condition.HashExists("myhash", "field1");
        var message = CreateConditionMessage(condition, RedisCommand.HEXISTS, "myhash", "field1");

        //Act
        var result = Execute(":1\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void exists_condition_hash_not_exists_true()
    {
        //Arrange
        var condition = Condition.HashNotExists("myhash", "field1");
        var message = CreateConditionMessage(condition, RedisCommand.HEXISTS, "myhash", "field1");

        //Act
        var result = Execute(":0\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void exists_condition_set_contains_true()
    {
        //Arrange
        var condition = Condition.SetContains("myset", "member1");
        var message = CreateConditionMessage(condition, RedisCommand.SISMEMBER, "myset", "member1");

        //Act
        var result = Execute(":1\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void exists_condition_set_not_contains_true()
    {
        //Arrange
        var condition = Condition.SetNotContains("myset", "member1");
        var message = CreateConditionMessage(condition, RedisCommand.SISMEMBER, "myset", "member1");

        //Act
        var result = Execute(":0\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void exists_condition_sorted_set_contains_true()
    {
        //Arrange
        var condition = Condition.SortedSetContains("myzset", "member1");
        var message = CreateConditionMessage(condition, RedisCommand.ZSCORE, "myzset", "member1");

        //Act
        var result = Execute("$1\r\n5\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void exists_condition_sorted_set_contains_null_false()
    {
        //Arrange
        var condition = Condition.SortedSetContains("myzset", "member1");
        var message = CreateConditionMessage(condition, RedisCommand.ZSCORE, "myzset", "member1");

        //Act
        var result = Execute("$-1\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void exists_condition_sorted_set_not_contains_true()
    {
        //Arrange
        var condition = Condition.SortedSetNotContains("myzset", "member1");
        var message = CreateConditionMessage(condition, RedisCommand.ZSCORE, "myzset", "member1");

        //Act
        var result = Execute("$-1\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void starts_with_condition_match_true()
    {
        //Arrange
        var condition = Condition.SortedSetContainsStarting("myzset", "pre");
        var message = CreateConditionMessage(condition, RedisCommand.ZRANGEBYLEX, "myzset", "[pre", "+", "LIMIT", 0, 1);

        //Act
        var result = Execute("*1\r\n$6\r\nprefix\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void starts_with_condition_no_match_false()
    {
        //Arrange
        var condition = Condition.SortedSetContainsStarting("myzset", "pre");
        var message = CreateConditionMessage(condition, RedisCommand.ZRANGEBYLEX, "myzset", "[pre", "+", "LIMIT", 0, 1);

        //Act
        var result = Execute("*0\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void starts_with_condition_not_contains_starting_true()
    {
        //Arrange
        var condition = Condition.SortedSetNotContainsStarting("myzset", "pre");
        var message = CreateConditionMessage(condition, RedisCommand.ZRANGEBYLEX, "myzset", "[pre", "+", "LIMIT", 0, 1);

        //Act
        var result = Execute("*0\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void equals_condition_string_equal_true()
    {
        //Arrange
        var condition = Condition.StringEqual("mykey", "value1");
        var message = CreateConditionMessage(condition, RedisCommand.GET, "mykey", RedisValue.Null);

        //Act
        var result = Execute("$6\r\nvalue1\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void equals_condition_string_equal_false()
    {
        //Arrange
        var condition = Condition.StringEqual("mykey", "value1");
        var message = CreateConditionMessage(condition, RedisCommand.GET, "mykey", RedisValue.Null);

        //Act
        var result = Execute("$6\r\nvalue2\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void equals_condition_string_not_equal_true()
    {
        //Arrange
        var condition = Condition.StringNotEqual("mykey", "value1");
        var message = CreateConditionMessage(condition, RedisCommand.GET, "mykey", RedisValue.Null);

        //Act
        var result = Execute("$6\r\nvalue2\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void equals_condition_hash_equal_true()
    {
        //Arrange
        var condition = Condition.HashEqual("myhash", "field1", "value1");
        var message = CreateConditionMessage(condition, RedisCommand.HGET, "myhash", "field1");

        //Act
        var result = Execute("$6\r\nvalue1\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void equals_condition_hash_not_equal_true()
    {
        //Arrange
        var condition = Condition.HashNotEqual("myhash", "field1", "value1");
        var message = CreateConditionMessage(condition, RedisCommand.HGET, "myhash", "field1");

        //Act
        var result = Execute("$6\r\nvalue2\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void equals_condition_sorted_set_equal_true()
    {
        //Arrange
        var condition = Condition.SortedSetEqual("myzset", "member1", 5.0);
        var message = CreateConditionMessage(condition, RedisCommand.ZSCORE, "myzset", "member1");

        //Act
        var result = Execute("$1\r\n5\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void equals_condition_sorted_set_equal_false()
    {
        //Arrange
        var condition = Condition.SortedSetEqual("myzset", "member1", 5.0);
        var message = CreateConditionMessage(condition, RedisCommand.ZSCORE, "myzset", "member1");

        //Act
        var result = Execute("$1\r\n3\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void equals_condition_sorted_set_not_equal_true()
    {
        //Arrange
        var condition = Condition.SortedSetNotEqual("myzset", "member1", 5.0);
        var message = CreateConditionMessage(condition, RedisCommand.ZSCORE, "myzset", "member1");

        //Act
        var result = Execute("$1\r\n3\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void list_condition_index_equal_true()
    {
        //Arrange
        var condition = Condition.ListIndexEqual("mylist", 0, "value1");
        var message = CreateConditionMessage(condition, RedisCommand.LINDEX, "mylist", 0);

        //Act
        var result = Execute("$6\r\nvalue1\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void list_condition_index_equal_false()
    {
        //Arrange
        var condition = Condition.ListIndexEqual("mylist", 0, "value1");
        var message = CreateConditionMessage(condition, RedisCommand.LINDEX, "mylist", 0);

        //Act
        var result = Execute("$6\r\nvalue2\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void list_condition_index_not_equal_true()
    {
        //Arrange
        var condition = Condition.ListIndexNotEqual("mylist", 0, "value1");
        var message = CreateConditionMessage(condition, RedisCommand.LINDEX, "mylist", 0);

        //Act
        var result = Execute("$6\r\nvalue2\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void list_condition_index_exists_true()
    {
        //Arrange
        var condition = Condition.ListIndexExists("mylist", 0);
        var message = CreateConditionMessage(condition, RedisCommand.LINDEX, "mylist", 0);

        //Act
        var result = Execute("$6\r\nvalue1\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void list_condition_index_exists_null_false()
    {
        //Arrange
        var condition = Condition.ListIndexExists("mylist", 0);
        var message = CreateConditionMessage(condition, RedisCommand.LINDEX, "mylist", 0);

        //Act
        var result = Execute("$-1\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void list_condition_index_not_exists_true()
    {
        //Arrange
        var condition = Condition.ListIndexNotExists("mylist", 0);
        var message = CreateConditionMessage(condition, RedisCommand.LINDEX, "mylist", 0);

        //Act
        var result = Execute("$-1\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void length_condition_string_length_equal_true()
    {
        //Arrange
        var condition = Condition.StringLengthEqual("mykey", 10);
        var message = CreateConditionMessage(condition, RedisCommand.STRLEN, "mykey");

        //Act
        var result = Execute(":10\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void length_condition_string_length_equal_false()
    {
        //Arrange
        var condition = Condition.StringLengthEqual("mykey", 10);
        var message = CreateConditionMessage(condition, RedisCommand.STRLEN, "mykey");

        //Act
        var result = Execute(":5\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void length_condition_string_length_less_than_true()
    {
        //Arrange
        var condition = Condition.StringLengthLessThan("mykey", 10);
        var message = CreateConditionMessage(condition, RedisCommand.STRLEN, "mykey");

        //Act
        var result = Execute(":5\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void length_condition_string_length_greater_than_true()
    {
        //Arrange
        var condition = Condition.StringLengthGreaterThan("mykey", 10);
        var message = CreateConditionMessage(condition, RedisCommand.STRLEN, "mykey");

        //Act
        var result = Execute(":15\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void length_condition_hash_length_equal_true()
    {
        //Arrange
        var condition = Condition.HashLengthEqual("myhash", 5);
        var message = CreateConditionMessage(condition, RedisCommand.HLEN, "myhash");

        //Act
        var result = Execute(":5\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void length_condition_list_length_equal_true()
    {
        //Arrange
        var condition = Condition.ListLengthEqual("mylist", 3);
        var message = CreateConditionMessage(condition, RedisCommand.LLEN, "mylist");

        //Act
        var result = Execute(":3\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void length_condition_set_length_equal_true()
    {
        //Arrange
        var condition = Condition.SetLengthEqual("myset", 7);
        var message = CreateConditionMessage(condition, RedisCommand.SCARD, "myset");

        //Act
        var result = Execute(":7\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void length_condition_sorted_set_length_equal_true()
    {
        //Arrange
        var condition = Condition.SortedSetLengthEqual("myzset", 4);
        var message = CreateConditionMessage(condition, RedisCommand.ZCARD, "myzset");

        //Act
        var result = Execute(":4\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void length_condition_stream_length_equal_true()
    {
        //Arrange
        var condition = Condition.StreamLengthEqual("mystream", 10);
        var message = CreateConditionMessage(condition, RedisCommand.XLEN, "mystream");

        //Act
        var result = Execute(":10\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void sorted_set_range_length_condition_equal_true()
    {
        //Arrange
        var condition = Condition.SortedSetLengthEqual("myzset", 5, 0, 10);
        var message = CreateConditionMessage(condition, RedisCommand.ZCOUNT, "myzset", 0, 10);

        //Act
        var result = Execute(":5\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void sorted_set_range_length_condition_less_than_true()
    {
        //Arrange
        var condition = Condition.SortedSetLengthLessThan("myzset", 10, 0, 100);
        var message = CreateConditionMessage(condition, RedisCommand.ZCOUNT, "myzset", 0, 100);

        //Act
        var result = Execute(":5\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void sorted_set_range_length_condition_greater_than_true()
    {
        //Arrange
        var condition = Condition.SortedSetLengthGreaterThan("myzset", 3, 0, 100);
        var message = CreateConditionMessage(condition, RedisCommand.ZCOUNT, "myzset", 0, 100);

        //Act
        var result = Execute(":10\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void sorted_set_score_condition_score_exists_true()
    {
        //Arrange
        var condition = Condition.SortedSetScoreExists("myzset", 5.0);
        var message = CreateConditionMessage(condition, RedisCommand.ZCOUNT, "myzset", 5.0, 5.0);

        //Act
        var result = Execute(":3\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void sorted_set_score_condition_score_exists_false()
    {
        //Arrange
        var condition = Condition.SortedSetScoreExists("myzset", 5.0);
        var message = CreateConditionMessage(condition, RedisCommand.ZCOUNT, "myzset", 5.0, 5.0);

        //Act
        var result = Execute(":0\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void sorted_set_score_condition_score_not_exists_true()
    {
        //Arrange
        var condition = Condition.SortedSetScoreNotExists("myzset", 5.0);
        var message = CreateConditionMessage(condition, RedisCommand.ZCOUNT, "myzset", 5.0, 5.0);

        //Act
        var result = Execute(":0\r\n", Condition.ConditionProcessor.Default, message);

        //Assert
        result.Should().BeTrue();
    }
}
