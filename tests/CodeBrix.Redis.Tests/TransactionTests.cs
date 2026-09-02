using System;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[RunPerProtocol]
public class TransactionTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    [Fact]
    public async Task basic_empty_tran()
    {
        await using var conn = Create();

        RedisKey key = Me();
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyExists(key).Should().BeFalse();

        var tran = db.CreateTransaction();

        var result = tran.Execute();
        result.Should().BeTrue();
    }

    [Fact]
    public async Task nested_transaction_throws()
    {
        //Arrange
        await using var conn = Create();

        var db = conn.GetDatabase();

        //Act
        var tran = db.CreateTransaction();

        //Assert
        var redisTransaction = Assert.IsType<RedisTransaction>(tran);
        Assert.Throws<NotSupportedException>(() => redisTransaction.CreateTransaction(null));
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public async Task basic_tran_with_exists_condition(bool demandKeyExists, bool keyExists, bool expectTranResult)
    {
        await using var conn = Create(disabledCommands: ["info", "config"]);

        RedisKey key = Me(), key2 = Me() + "2";
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyDelete(key2, CommandFlags.FireAndForget);
        if (keyExists) db.StringSet(key2, "any value", flags: CommandFlags.FireAndForget);
        db.KeyExists(key).Should().BeFalse();
        db.KeyExists(key2).Should().Be(keyExists);

        var tran = db.CreateTransaction();
        var cond = tran.AddCondition(demandKeyExists ? Condition.KeyExists(key2) : Condition.KeyNotExists(key2));
        var incr = tran.StringIncrementAsync(key);
        var exec = tran.ExecuteAsync();
        var get = db.StringGet(key);

        (await exec).Should().Be(expectTranResult);
        if (demandKeyExists == keyExists)
        {
            (await exec).Should().BeTrue("eq: exec");
            cond.WasSatisfied.Should().BeTrue("eq: was satisfied");
            (await incr).Should().Be(1); // eq: incr
            ((long)get).Should().Be(1); // eq: get
        }
        else
        {
            (await exec).Should().BeFalse("neq: exec");
            cond.WasSatisfied.Should().BeFalse("neq: was satisfied");
            SafeStatus(incr).Should().Be(TaskStatus.Canceled); // neq: incr
            ((long)get).Should().Be(0); // neq: get
        }
    }

    [Theory]
    [InlineData("same", "same", true, true)]
    [InlineData("x", "y", true, false)]
    [InlineData("x", null, true, false)]
    [InlineData(null, "y", true, false)]
    [InlineData(null, null, true, true)]

    [InlineData("same", "same", false, false)]
    [InlineData("x", "y", false, true)]
    [InlineData("x", null, false, true)]
    [InlineData(null, "y", false, true)]
    [InlineData(null, null, false, false)]
    public async Task basic_tran_with_equals_condition(string? expected, string? value, bool expectEqual, bool expectTranResult)
    {
        await using var conn = Create();

        RedisKey key = Me(), key2 = Me() + "2";
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyDelete(key2, CommandFlags.FireAndForget);

        if (value != null) db.StringSet(key2, value, flags: CommandFlags.FireAndForget);
        db.KeyExists(key).Should().BeFalse();
        db.StringGet(key2).Should().Be(value);

        var tran = db.CreateTransaction();
        var cond = tran.AddCondition(expectEqual ? Condition.StringEqual(key2, expected) : Condition.StringNotEqual(key2, expected));
        var incr = tran.StringIncrementAsync(key);
        var exec = tran.ExecuteAsync();
        var get = db.StringGet(key);

        (await exec).Should().Be(expectTranResult);
        if (expectEqual == (value == expected))
        {
            (await exec).Should().BeTrue("eq: exec");
            cond.WasSatisfied.Should().BeTrue("eq: was satisfied");
            (await incr).Should().Be(1); // eq: incr
            ((long)get).Should().Be(1); // eq: get
        }
        else
        {
            (await exec).Should().BeFalse("neq: exec");
            cond.WasSatisfied.Should().BeFalse("neq: was satisfied");
            SafeStatus(incr).Should().Be(TaskStatus.Canceled); // neq: incr
            ((long)get).Should().Be(0); // neq: get
        }
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public async Task basic_tran_with_hash_exists_condition(bool demandKeyExists, bool keyExists, bool expectTranResult)
    {
        await using var conn = Create(disabledCommands: ["info", "config"]);

        RedisKey key = Me(), key2 = Me() + "2";
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyDelete(key2, CommandFlags.FireAndForget);
        RedisValue hashField = "field";
        if (keyExists) db.HashSet(key2, hashField, "any value", flags: CommandFlags.FireAndForget);
        db.KeyExists(key).Should().BeFalse();
        db.HashExists(key2, hashField).Should().Be(keyExists);

        var tran = db.CreateTransaction();
        var cond = tran.AddCondition(demandKeyExists ? Condition.HashExists(key2, hashField) : Condition.HashNotExists(key2, hashField));
        var incr = tran.StringIncrementAsync(key);
        var exec = tran.ExecuteAsync();
        var get = db.StringGet(key);

        (await exec).Should().Be(expectTranResult);
        if (demandKeyExists == keyExists)
        {
            (await exec).Should().BeTrue("eq: exec");
            cond.WasSatisfied.Should().BeTrue("eq: was satisfied");
            (await incr).Should().Be(1); // eq: incr
            ((long)get).Should().Be(1); // eq: get
        }
        else
        {
            (await exec).Should().BeFalse("neq: exec");
            cond.WasSatisfied.Should().BeFalse("neq: was satisfied");
            SafeStatus(incr).Should().Be(TaskStatus.Canceled); // neq: incr
            ((long)get).Should().Be(0); // neq: get
        }
    }

    [Theory]
    [InlineData("same", "same", true, true)]
    [InlineData("x", "y", true, false)]
    [InlineData("x", null, true, false)]
    [InlineData(null, "y", true, false)]
    [InlineData(null, null, true, true)]

    [InlineData("same", "same", false, false)]
    [InlineData("x", "y", false, true)]
    [InlineData("x", null, false, true)]
    [InlineData(null, "y", false, true)]
    [InlineData(null, null, false, false)]
    public async Task basic_tran_with_hash_equals_condition(string? expected, string? value, bool expectEqual, bool expectedTranResult)
    {
        await using var conn = Create();

        RedisKey key = Me(), key2 = Me() + "2";
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyDelete(key2, CommandFlags.FireAndForget);

        RedisValue hashField = "field";
        if (value != null) db.HashSet(key2, hashField, value, flags: CommandFlags.FireAndForget);
        db.KeyExists(key).Should().BeFalse();
        db.HashGet(key2, hashField).Should().Be(value);

        var tran = db.CreateTransaction();
        var cond = tran.AddCondition(expectEqual ? Condition.HashEqual(key2, hashField, expected) : Condition.HashNotEqual(key2, hashField, expected));
        var incr = tran.StringIncrementAsync(key);
        var exec = tran.ExecuteAsync();
        var get = db.StringGet(key);

        (await exec).Should().Be(expectedTranResult);
        if (expectEqual == (value == expected))
        {
            (await exec).Should().BeTrue("eq: exec");
            cond.WasSatisfied.Should().BeTrue("eq: was satisfied");
            (await incr).Should().Be(1); // eq: incr
            ((long)get).Should().Be(1); // eq: get
        }
        else
        {
            (await exec).Should().BeFalse("neq: exec");
            cond.WasSatisfied.Should().BeFalse("neq: was satisfied");
            SafeStatus(incr).Should().Be(TaskStatus.Canceled); // neq: incr
            ((long)get).Should().Be(0); // neq: get
        }
    }

    private static TaskStatus SafeStatus(Task task)
    {
        if (task.Status == TaskStatus.WaitingForActivation)
        {
            try
            {
                if (!task.Wait(1000)) throw new TimeoutException("timeout waiting for task to complete");
            }
            catch (AggregateException ex)
            when (ex.InnerException is TaskCanceledException
                || (ex.InnerExceptions.Count == 1 && ex.InnerException is TaskCanceledException))
            {
                return TaskStatus.Canceled;
            }
            catch (TaskCanceledException)
            {
                return TaskStatus.Canceled;
            }
        }
        return task.Status;
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public async Task basic_tran_with_list_exists_condition(bool demandKeyExists, bool keyExists, bool expectTranResult)
    {
        await using var conn = Create(disabledCommands: ["info", "config"]);

        RedisKey key = Me(), key2 = Me() + "2";
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyDelete(key2, CommandFlags.FireAndForget);
        if (keyExists) db.ListRightPush(key2, "any value", flags: CommandFlags.FireAndForget);
        db.KeyExists(key).Should().BeFalse();
        db.KeyExists(key2).Should().Be(keyExists);

        var tran = db.CreateTransaction();
        var cond = tran.AddCondition(demandKeyExists ? Condition.ListIndexExists(key2, 0) : Condition.ListIndexNotExists(key2, 0));
        var push = tran.ListRightPushAsync(key, "any value");
        var exec = tran.ExecuteAsync();
        var get = db.ListGetByIndex(key, 0);

        (await exec).Should().Be(expectTranResult);
        if (demandKeyExists == keyExists)
        {
            (await exec).Should().BeTrue("eq: exec");
            cond.WasSatisfied.Should().BeTrue("eq: was satisfied");
            (await push).Should().Be(1); // eq: push
            get.Should().Be("any value"); // eq: get
        }
        else
        {
            (await exec).Should().BeFalse("neq: exec");
            cond.WasSatisfied.Should().BeFalse("neq: was satisfied");
            SafeStatus(push).Should().Be(TaskStatus.Canceled); // neq: push
            ((string?)get).Should().BeNull(); // neq: get
        }
    }

    [Theory]
    [InlineData("same", "same", true, true)]
    [InlineData("x", "y", true, false)]
    [InlineData("x", null, true, false)]
    [InlineData(null, "y", true, false)]
    [InlineData(null, null, true, true)]

    [InlineData("same", "same", false, false)]
    [InlineData("x", "y", false, true)]
    [InlineData("x", null, false, true)]
    [InlineData(null, "y", false, true)]
    [InlineData(null, null, false, false)]
    public async Task basic_tran_with_list_equals_condition(string? expected, string? value, bool expectEqual, bool expectTranResult)
    {
        await using var conn = Create();

        RedisKey key = Me(), key2 = Me() + "2";
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyDelete(key2, CommandFlags.FireAndForget);

        if (value != null) db.ListRightPush(key2, value, flags: CommandFlags.FireAndForget);
        db.KeyExists(key).Should().BeFalse();
        db.ListGetByIndex(key2, 0).Should().Be(value);

        var tran = db.CreateTransaction();
        var cond = tran.AddCondition(expectEqual ? Condition.ListIndexEqual(key2, 0, expected) : Condition.ListIndexNotEqual(key2, 0, expected));
        var push = tran.ListRightPushAsync(key, "any value");
        var exec = tran.ExecuteAsync();
        var get = db.ListGetByIndex(key, 0);

        (await exec).Should().Be(expectTranResult);
        if (expectEqual == (value == expected))
        {
            (await exec).Should().BeTrue("eq: exec");
            cond.WasSatisfied.Should().BeTrue("eq: was satisfied");
            (await push).Should().Be(1); // eq: push
            get.Should().Be("any value"); // eq: get
        }
        else
        {
            (await exec).Should().BeFalse("neq: exec");
            cond.WasSatisfied.Should().BeFalse("neq: was satisfied");
            SafeStatus(push).Should().Be(TaskStatus.Canceled); // neq: push
            ((string?)get).Should().BeNull(); // neq: get
        }
    }

    public enum ComparisonType
    {
        Equal,
        LessThan,
        GreaterThan,
    }

    [Theory]
    [InlineData("five", ComparisonType.Equal, 5L, false)]
    [InlineData("four", ComparisonType.Equal, 4L, true)]
    [InlineData("three", ComparisonType.Equal, 3L, false)]
    [InlineData("", ComparisonType.Equal, 2L, false)]
    [InlineData("", ComparisonType.Equal, 0L, true)]
    [InlineData(null, ComparisonType.Equal, 1L, false)]
    [InlineData(null, ComparisonType.Equal, 0L, true)]

    [InlineData("five", ComparisonType.LessThan, 5L, true)]
    [InlineData("four", ComparisonType.LessThan, 4L, false)]
    [InlineData("three", ComparisonType.LessThan, 3L, false)]
    [InlineData("", ComparisonType.LessThan, 2L, true)]
    [InlineData("", ComparisonType.LessThan, 0L, false)]
    [InlineData(null, ComparisonType.LessThan, 1L, true)]
    [InlineData(null, ComparisonType.LessThan, 0L, false)]

    [InlineData("five", ComparisonType.GreaterThan, 5L, false)]
    [InlineData("four", ComparisonType.GreaterThan, 4L, false)]
    [InlineData("three", ComparisonType.GreaterThan, 3L, true)]
    [InlineData("", ComparisonType.GreaterThan, 2L, false)]
    [InlineData("", ComparisonType.GreaterThan, 0L, false)]
    [InlineData(null, ComparisonType.GreaterThan, 1L, false)]
    [InlineData(null, ComparisonType.GreaterThan, 0L, false)]
    public async Task basic_tran_with_string_length_condition(string? value, ComparisonType type, long length, bool expectTranResult)
    {
        await using var conn = Create();

        RedisKey key = Me(), key2 = Me() + "2";
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyDelete(key2, CommandFlags.FireAndForget);

        bool expectSuccess;
        Condition? condition;
        var valueLength = value?.Length ?? 0;
        switch (type)
        {
            case ComparisonType.Equal:
                expectSuccess = valueLength == length;
                condition = Condition.StringLengthEqual(key2, length);
                condition.ToString().Should().Contain("String length == " + length);
                break;
            case ComparisonType.GreaterThan:
                expectSuccess = valueLength > length;
                condition = Condition.StringLengthGreaterThan(key2, length);
                condition.ToString().Should().Contain("String length > " + length);
                break;
            case ComparisonType.LessThan:
                expectSuccess = valueLength < length;
                condition = Condition.StringLengthLessThan(key2, length);
                condition.ToString().Should().Contain("String length < " + length);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }

        if (value != null) db.StringSet(key2, value, flags: CommandFlags.FireAndForget);
        db.KeyExists(key).Should().BeFalse();
        db.StringGet(key2).Should().Be(value);

        var tran = db.CreateTransaction();
        var cond = tran.AddCondition(condition);
        var push = tran.StringSetAsync(key, "any value");
        var exec = tran.ExecuteAsync();
        var get = db.StringLength(key);

        (await exec).Should().Be(expectTranResult);

        if (expectSuccess)
        {
            (await exec).Should().BeTrue("eq: exec");
            cond.WasSatisfied.Should().BeTrue("eq: was satisfied");
            (await push).Should().BeTrue(); // eq: push
            get.Should().Be("any value".Length); // eq: get
        }
        else
        {
            (await exec).Should().BeFalse("neq: exec");
            cond.WasSatisfied.Should().BeFalse("neq: was satisfied");
            SafeStatus(push).Should().Be(TaskStatus.Canceled); // neq: push
            get.Should().Be(0); // neq: get
        }
    }

    [Theory]
    [InlineData("five", ComparisonType.Equal, 5L, false)]
    [InlineData("four", ComparisonType.Equal, 4L, true)]
    [InlineData("three", ComparisonType.Equal, 3L, false)]
    [InlineData("", ComparisonType.Equal, 2L, false)]
    [InlineData("", ComparisonType.Equal, 0L, true)]

    [InlineData("five", ComparisonType.LessThan, 5L, true)]
    [InlineData("four", ComparisonType.LessThan, 4L, false)]
    [InlineData("three", ComparisonType.LessThan, 3L, false)]
    [InlineData("", ComparisonType.LessThan, 2L, true)]
    [InlineData("", ComparisonType.LessThan, 0L, false)]

    [InlineData("five", ComparisonType.GreaterThan, 5L, false)]
    [InlineData("four", ComparisonType.GreaterThan, 4L, false)]
    [InlineData("three", ComparisonType.GreaterThan, 3L, true)]
    [InlineData("", ComparisonType.GreaterThan, 2L, false)]
    [InlineData("", ComparisonType.GreaterThan, 0L, false)]
    public async Task basic_tran_with_hash_length_condition(string value, ComparisonType type, long length, bool expectTranResult)
    {
        await using var conn = Create();

        RedisKey key = Me(), key2 = Me() + "2";
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyDelete(key2, CommandFlags.FireAndForget);

        bool expectSuccess;
        Condition? condition;
        var valueLength = value?.Length ?? 0;
        switch (type)
        {
            case ComparisonType.Equal:
                expectSuccess = valueLength == length;
                condition = Condition.HashLengthEqual(key2, length);
                break;
            case ComparisonType.GreaterThan:
                expectSuccess = valueLength > length;
                condition = Condition.HashLengthGreaterThan(key2, length);
                break;
            case ComparisonType.LessThan:
                expectSuccess = valueLength < length;
                condition = Condition.HashLengthLessThan(key2, length);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }

        for (var i = 0; i < valueLength; i++)
        {
            db.HashSet(key2, i, value![i].ToString(), flags: CommandFlags.FireAndForget);
        }
        db.KeyExists(key).Should().BeFalse();
        db.HashLength(key2).Should().Be(valueLength);

        var tran = db.CreateTransaction();
        var cond = tran.AddCondition(condition);
        var push = tran.StringSetAsync(key, "any value");
        var exec = tran.ExecuteAsync();
        var get = db.StringLength(key);

        (await exec).Should().Be(expectTranResult);

        if (expectSuccess)
        {
            (await exec).Should().BeTrue("eq: exec");
            cond.WasSatisfied.Should().BeTrue("eq: was satisfied");
            (await push).Should().BeTrue(); // eq: push
            get.Should().Be("any value".Length); // eq: get
        }
        else
        {
            (await exec).Should().BeFalse("neq: exec");
            cond.WasSatisfied.Should().BeFalse("neq: was satisfied");
            SafeStatus(push).Should().Be(TaskStatus.Canceled); // neq: push
            get.Should().Be(0); // neq: get
        }
    }

    [Theory]
    [InlineData("five", ComparisonType.Equal, 5L, false)]
    [InlineData("four", ComparisonType.Equal, 4L, true)]
    [InlineData("three", ComparisonType.Equal, 3L, false)]
    [InlineData("", ComparisonType.Equal, 2L, false)]
    [InlineData("", ComparisonType.Equal, 0L, true)]

    [InlineData("five", ComparisonType.LessThan, 5L, true)]
    [InlineData("four", ComparisonType.LessThan, 4L, false)]
    [InlineData("three", ComparisonType.LessThan, 3L, false)]
    [InlineData("", ComparisonType.LessThan, 2L, true)]
    [InlineData("", ComparisonType.LessThan, 0L, false)]

    [InlineData("five", ComparisonType.GreaterThan, 5L, false)]
    [InlineData("four", ComparisonType.GreaterThan, 4L, false)]
    [InlineData("three", ComparisonType.GreaterThan, 3L, true)]
    [InlineData("", ComparisonType.GreaterThan, 2L, false)]
    [InlineData("", ComparisonType.GreaterThan, 0L, false)]
    public async Task basic_tran_with_set_cardinality_condition(string value, ComparisonType type, long length, bool expectTranResult)
    {
        await using var conn = Create();

        RedisKey key = Me(), key2 = Me() + "2";
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyDelete(key2, CommandFlags.FireAndForget);

        bool expectSuccess;
        Condition? condition;
        var valueLength = value?.Length ?? 0;
        switch (type)
        {
            case ComparisonType.Equal:
                expectSuccess = valueLength == length;
                condition = Condition.SetLengthEqual(key2, length);
                break;
            case ComparisonType.GreaterThan:
                expectSuccess = valueLength > length;
                condition = Condition.SetLengthGreaterThan(key2, length);
                break;
            case ComparisonType.LessThan:
                expectSuccess = valueLength < length;
                condition = Condition.SetLengthLessThan(key2, length);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }

        for (var i = 0; i < valueLength; i++)
        {
            db.SetAdd(key2, i, flags: CommandFlags.FireAndForget);
        }
        db.KeyExists(key).Should().BeFalse();
        db.SetLength(key2).Should().Be(valueLength);

        var tran = db.CreateTransaction();
        var cond = tran.AddCondition(condition);
        var push = tran.StringSetAsync(key, "any value");
        var exec = tran.ExecuteAsync();
        var get = db.StringLength(key);

        (await exec).Should().Be(expectTranResult);

        if (expectSuccess)
        {
            (await exec).Should().BeTrue("eq: exec");
            cond.WasSatisfied.Should().BeTrue("eq: was satisfied");
            (await push).Should().BeTrue(); // eq: push
            get.Should().Be("any value".Length); // eq: get
        }
        else
        {
            (await exec).Should().BeFalse("neq: exec");
            cond.WasSatisfied.Should().BeFalse("neq: was satisfied");
            SafeStatus(push).Should().Be(TaskStatus.Canceled); // neq: push
            get.Should().Be(0); // neq: get
        }
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public async Task basic_tran_with_set_contains_condition(bool demandKeyExists, bool keyExists, bool expectTranResult)
    {
        await using var conn = Create(disabledCommands: ["info", "config"]);

        RedisKey key = Me(), key2 = Me() + "2";
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyDelete(key2, CommandFlags.FireAndForget);
        RedisValue member = "value";
        if (keyExists) db.SetAdd(key2, member, flags: CommandFlags.FireAndForget);
        db.KeyExists(key).Should().BeFalse();
        db.SetContains(key2, member).Should().Be(keyExists);

        var tran = db.CreateTransaction();
        var cond = tran.AddCondition(demandKeyExists ? Condition.SetContains(key2, member) : Condition.SetNotContains(key2, member));
        var incr = tran.StringIncrementAsync(key);
        var exec = tran.ExecuteAsync();
        var get = db.StringGet(key);

        (await exec).Should().Be(expectTranResult);
        if (demandKeyExists == keyExists)
        {
            (await exec).Should().BeTrue("eq: exec");
            cond.WasSatisfied.Should().BeTrue("eq: was satisfied");
            (await incr).Should().Be(1); // eq: incr
            ((long)get).Should().Be(1); // eq: get
        }
        else
        {
            (await exec).Should().BeFalse("neq: exec");
            cond.WasSatisfied.Should().BeFalse("neq: was satisfied");
            SafeStatus(incr).Should().Be(TaskStatus.Canceled); // neq: incr
            ((long)get).Should().Be(0); // neq: get
        }
    }

    [Theory]
    [InlineData("five", ComparisonType.Equal, 5L, false)]
    [InlineData("four", ComparisonType.Equal, 4L, true)]
    [InlineData("three", ComparisonType.Equal, 3L, false)]
    [InlineData("", ComparisonType.Equal, 2L, false)]
    [InlineData("", ComparisonType.Equal, 0L, true)]

    [InlineData("five", ComparisonType.LessThan, 5L, true)]
    [InlineData("four", ComparisonType.LessThan, 4L, false)]
    [InlineData("three", ComparisonType.LessThan, 3L, false)]
    [InlineData("", ComparisonType.LessThan, 2L, true)]
    [InlineData("", ComparisonType.LessThan, 0L, false)]

    [InlineData("five", ComparisonType.GreaterThan, 5L, false)]
    [InlineData("four", ComparisonType.GreaterThan, 4L, false)]
    [InlineData("three", ComparisonType.GreaterThan, 3L, true)]
    [InlineData("", ComparisonType.GreaterThan, 2L, false)]
    [InlineData("", ComparisonType.GreaterThan, 0L, false)]
    public async Task basic_tran_with_sorted_set_cardinality_condition(string value, ComparisonType type, long length, bool expectTranResult)
    {
        await using var conn = Create();

        RedisKey key = Me(), key2 = Me() + "2";
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyDelete(key2, CommandFlags.FireAndForget);

        bool expectSuccess;
        Condition? condition;
        var valueLength = value?.Length ?? 0;
        switch (type)
        {
            case ComparisonType.Equal:
                expectSuccess = valueLength == length;
                condition = Condition.SortedSetLengthEqual(key2, length);
                break;
            case ComparisonType.GreaterThan:
                expectSuccess = valueLength > length;
                condition = Condition.SortedSetLengthGreaterThan(key2, length);
                break;
            case ComparisonType.LessThan:
                expectSuccess = valueLength < length;
                condition = Condition.SortedSetLengthLessThan(key2, length);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }

        for (var i = 0; i < valueLength; i++)
        {
            db.SortedSetAdd(key2, i, i, flags: CommandFlags.FireAndForget);
        }
        db.KeyExists(key).Should().BeFalse();
        db.SortedSetLength(key2).Should().Be(valueLength);

        var tran = db.CreateTransaction();
        var cond = tran.AddCondition(condition);
        var push = tran.StringSetAsync(key, "any value");
        var exec = tran.ExecuteAsync();
        var get = db.StringLength(key);

        (await exec).Should().Be(expectTranResult);

        if (expectSuccess)
        {
            (await exec).Should().BeTrue("eq: exec");
            cond.WasSatisfied.Should().BeTrue("eq: was satisfied");
            (await push).Should().BeTrue(); // eq: push
            get.Should().Be("any value".Length); // eq: get
        }
        else
        {
            (await exec).Should().BeFalse("neq: exec");
            cond.WasSatisfied.Should().BeFalse("neq: was satisfied");
            SafeStatus(push).Should().Be(TaskStatus.Canceled); // neq: push
            get.Should().Be(0); // neq: get
        }
    }

    [Theory]
    [InlineData(1, 4, ComparisonType.Equal, 5L, false)]
    [InlineData(1, 4, ComparisonType.Equal, 4L, true)]
    [InlineData(1, 2, ComparisonType.Equal, 3L, false)]
    [InlineData(1, 1, ComparisonType.Equal, 2L, false)]
    [InlineData(0, 0, ComparisonType.Equal, 0L, false)]

    [InlineData(1, 4, ComparisonType.LessThan, 5L, true)]
    [InlineData(1, 4, ComparisonType.LessThan, 4L, false)]
    [InlineData(1, 3, ComparisonType.LessThan, 3L, false)]
    [InlineData(1, 1, ComparisonType.LessThan, 2L, true)]
    [InlineData(0, 0, ComparisonType.LessThan, 0L, false)]

    [InlineData(1, 5, ComparisonType.GreaterThan, 5L, false)]
    [InlineData(1, 4, ComparisonType.GreaterThan, 4L, false)]
    [InlineData(1, 4, ComparisonType.GreaterThan, 3L, true)]
    [InlineData(1, 2, ComparisonType.GreaterThan, 2L, false)]
    [InlineData(0, 0, ComparisonType.GreaterThan, 0L, true)]
    public async Task basic_tran_with_sorted_set_range_count_condition(double min, double max, ComparisonType type, long length, bool expectTranResult)
    {
        await using var conn = Create();

        RedisKey key = Me(), key2 = Me() + "2";
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyDelete(key2, CommandFlags.FireAndForget);

        bool expectSuccess;
        Condition? condition;
        var valueLength = (int)(max - min) + 1;
        switch (type)
        {
            case ComparisonType.Equal:
                expectSuccess = valueLength == length;
                condition = Condition.SortedSetLengthEqual(key2, length, min, max);
                break;
            case ComparisonType.GreaterThan:
                expectSuccess = valueLength > length;
                condition = Condition.SortedSetLengthGreaterThan(key2, length, min, max);
                break;
            case ComparisonType.LessThan:
                expectSuccess = valueLength < length;
                condition = Condition.SortedSetLengthLessThan(key2, length, min, max);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }

        for (var i = 0; i < 5; i++)
        {
            db.SortedSetAdd(key2, i, i, flags: CommandFlags.FireAndForget);
        }
        db.KeyExists(key).Should().BeFalse();
        db.SortedSetLength(key2).Should().Be(5);

        var tran = db.CreateTransaction();
        var cond = tran.AddCondition(condition);
        var push = tran.StringSetAsync(key, "any value");
        var exec = tran.ExecuteAsync();
        var get = db.StringLength(key);

        (await exec).Should().Be(expectTranResult);

        if (expectSuccess)
        {
            (await exec).Should().BeTrue("eq: exec");
            cond.WasSatisfied.Should().BeTrue("eq: was satisfied");
            (await push).Should().BeTrue(); // eq: push
            get.Should().Be("any value".Length); // eq: get
        }
        else
        {
            (await exec).Should().BeFalse("neq: exec");
            cond.WasSatisfied.Should().BeFalse("neq: was satisfied");
            SafeStatus(push).Should().Be(TaskStatus.Canceled); // neq: push
            get.Should().Be(0); // neq: get
        }
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public async Task basic_tran_with_sorted_set_contains_condition(bool demandKeyExists, bool keyExists, bool expectTranResult)
    {
        await using var conn = Create(disabledCommands: ["info", "config"]);

        RedisKey key = Me(), key2 = Me() + "2";
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyDelete(key2, CommandFlags.FireAndForget);
        RedisValue member = "value";
        if (keyExists) db.SortedSetAdd(key2, member, 0.0, flags: CommandFlags.FireAndForget);
        db.KeyExists(key).Should().BeFalse();
        db.SortedSetScore(key2, member).HasValue.Should().Be(keyExists);

        var tran = db.CreateTransaction();
        var cond = tran.AddCondition(demandKeyExists ? Condition.SortedSetContains(key2, member) : Condition.SortedSetNotContains(key2, member));
        var incr = tran.StringIncrementAsync(key);
        var exec = tran.ExecuteAsync();
        var get = db.StringGet(key);

        (await exec).Should().Be(expectTranResult);
        if (demandKeyExists == keyExists)
        {
            (await exec).Should().BeTrue("eq: exec");
            cond.WasSatisfied.Should().BeTrue("eq: was satisfied");
            (await incr).Should().Be(1); // eq: incr
            ((long)get).Should().Be(1); // eq: get
        }
        else
        {
            (await exec).Should().BeFalse("neq: exec");
            cond.WasSatisfied.Should().BeFalse("neq: was satisfied");
            SafeStatus(incr).Should().Be(TaskStatus.Canceled); // neq: incr
            ((long)get).Should().Be(0); // neq: get
        }
    }

    public enum SortedSetValue
    {
        None,
        Exact,
        Shorter,
        Longer,
    }

    [Theory]
    [InlineData(false, SortedSetValue.None, true)]
    [InlineData(false, SortedSetValue.Shorter, true)]
    [InlineData(false, SortedSetValue.Exact, false)]
    [InlineData(false, SortedSetValue.Longer, false)]
    [InlineData(true, SortedSetValue.None, false)]
    [InlineData(true, SortedSetValue.Shorter, false)]
    [InlineData(true, SortedSetValue.Exact, true)]
    [InlineData(true, SortedSetValue.Longer, true)]
    public async Task basic_tran_with_sorted_set_starts_with_condition_string(bool requestExists, SortedSetValue existingValue, bool expectTranResult)
    {
        using var conn = Create();

        RedisKey key1 = Me() + "_1", key2 = Me() + "_2";
        var db = conn.GetDatabase();
        db.KeyDelete(key1, CommandFlags.FireAndForget);
        db.KeyDelete(key2, CommandFlags.FireAndForget);

        db.SortedSetAdd(key2, "unrelated", 0.0, flags: CommandFlags.FireAndForget);
        switch (existingValue)
        {
            case SortedSetValue.Shorter:
                db.SortedSetAdd(key2, "see", 0.0, flags: CommandFlags.FireAndForget);
                break;
            case SortedSetValue.Exact:
                db.SortedSetAdd(key2, "seek", 0.0, flags: CommandFlags.FireAndForget);
                break;
            case SortedSetValue.Longer:
                db.SortedSetAdd(key2, "seeks", 0.0, flags: CommandFlags.FireAndForget);
                break;
        }

        var tran = db.CreateTransaction();
        var cond = tran.AddCondition(requestExists ? Condition.SortedSetContainsStarting(key2, "seek") : Condition.SortedSetNotContainsStarting(key2, "seek"));
        var incr = tran.StringIncrementAsync(key1);
        var exec = await tran.ExecuteAsync();
        var get = await db.StringGetAsync(key1);

        exec.Should().Be(expectTranResult);
        cond.WasSatisfied.Should().Be(expectTranResult);

        if (expectTranResult)
        {
            (await incr).Should().Be(1); // eq: incr
            ((long)get).Should().Be(1); // eq: get
        }
        else
        {
            SafeStatus(incr).Should().Be(TaskStatus.Canceled); // neq: incr
            ((long)get).Should().Be(0); // neq: get
        }
    }

    [Theory]
    [InlineData(false, SortedSetValue.None, true)]
    [InlineData(false, SortedSetValue.Shorter, true)]
    [InlineData(false, SortedSetValue.Exact, false)]
    [InlineData(false, SortedSetValue.Longer, false)]
    [InlineData(true, SortedSetValue.None, false)]
    [InlineData(true, SortedSetValue.Shorter, false)]
    [InlineData(true, SortedSetValue.Exact, true)]
    [InlineData(true, SortedSetValue.Longer, true)]
    public async Task basic_tran_with_sorted_set_starts_with_condition_integer(bool requestExists, SortedSetValue existingValue, bool expectTranResult)
    {
        using var conn = Create();

        RedisKey key1 = Me() + "_1", key2 = Me() + "_2";
        var db = conn.GetDatabase();
        db.KeyDelete(key1, CommandFlags.FireAndForget);
        db.KeyDelete(key2, CommandFlags.FireAndForget);

        db.SortedSetAdd(key2, 789, 0.0, flags: CommandFlags.FireAndForget);
        switch (existingValue)
        {
            case SortedSetValue.Shorter:
                db.SortedSetAdd(key2, 123, 0.0, flags: CommandFlags.FireAndForget);
                break;
            case SortedSetValue.Exact:
                db.SortedSetAdd(key2, 1234, 0.0, flags: CommandFlags.FireAndForget);
                break;
            case SortedSetValue.Longer:
                db.SortedSetAdd(key2, 12345, 0.0, flags: CommandFlags.FireAndForget);
                break;
        }

        var tran = db.CreateTransaction();
        var cond = tran.AddCondition(requestExists ? Condition.SortedSetContainsStarting(key2, 1234) : Condition.SortedSetNotContainsStarting(key2, 1234));
        var incr = tran.StringIncrementAsync(key1);
        var exec = await tran.ExecuteAsync();
        var get = await db.StringGetAsync(key1);

        exec.Should().Be(expectTranResult);
        cond.WasSatisfied.Should().Be(expectTranResult);

        if (expectTranResult)
        {
            (await incr).Should().Be(1); // eq: incr
            ((long)get).Should().Be(1); // eq: get
        }
        else
        {
            SafeStatus(incr).Should().Be(TaskStatus.Canceled); // neq: incr
            ((long)get).Should().Be(0); // neq: get
        }
    }

    [Theory]
    [InlineData(4D, 4D, true, true)]
    [InlineData(4D, 5D, true, false)]
    [InlineData(4D, null, true, false)]
    [InlineData(null, 5D, true, false)]
    [InlineData(null, null, true, true)]

    [InlineData(4D, 4D, false, false)]
    [InlineData(4D, 5D, false, true)]
    [InlineData(4D, null, false, true)]
    [InlineData(null, 5D, false, true)]
    [InlineData(null, null, false, false)]
    public async Task basic_tran_with_sorted_set_equal_condition(double? expected, double? value, bool expectEqual, bool expectedTranResult)
    {
        await using var conn = Create();

        RedisKey key = Me(), key2 = Me() + "2";
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyDelete(key2, CommandFlags.FireAndForget);

        RedisValue member = "member";
        if (value != null) db.SortedSetAdd(key2, member, value.Value, flags: CommandFlags.FireAndForget);
        db.KeyExists(key).Should().BeFalse();
        db.SortedSetScore(key2, member).Should().Be(value);

        var tran = db.CreateTransaction();
        var cond = tran.AddCondition(expectEqual ? Condition.SortedSetEqual(key2, member, expected) : Condition.SortedSetNotEqual(key2, member, expected));
        var incr = tran.StringIncrementAsync(key);
        var exec = tran.ExecuteAsync();
        var get = db.StringGet(key);

        (await exec).Should().Be(expectedTranResult);
        if (expectEqual == (value == expected))
        {
            (await exec).Should().BeTrue("eq: exec");
            cond.WasSatisfied.Should().BeTrue("eq: was satisfied");
            (await incr).Should().Be(1); // eq: incr
            ((long)get).Should().Be(1); // eq: get
        }
        else
        {
            (await exec).Should().BeFalse("neq: exec");
            cond.WasSatisfied.Should().BeFalse("neq: was satisfied");
            SafeStatus(incr).Should().Be(TaskStatus.Canceled); // neq: incr
            ((long)get).Should().Be(0); // neq: get
        }
    }

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(false, true, true, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public async Task basic_tran_with_sorted_set_score_exists_condition(bool member1HasScore, bool member2HasScore, bool demandScoreExists, bool expectedTranResult)
    {
        await using var conn = Create();

        RedisKey key = Me(), key2 = Me() + "2";
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyDelete(key2, CommandFlags.FireAndForget);

        const double Score = 4D;
        RedisValue member1 = "member1";
        RedisValue member2 = "member2";
        if (member1HasScore)
        {
            db.SortedSetAdd(key2, member1, Score, flags: CommandFlags.FireAndForget);
        }

        if (member2HasScore)
        {
            db.SortedSetAdd(key2, member2, Score, flags: CommandFlags.FireAndForget);
        }

        db.KeyExists(key).Should().BeFalse();
        db.SortedSetScore(key2, member1).Should().Be(member1HasScore ? Score : null);
        db.SortedSetScore(key2, member2).Should().Be(member2HasScore ? Score : null);

        var tran = db.CreateTransaction();
        var cond = tran.AddCondition(demandScoreExists ? Condition.SortedSetScoreExists(key2, Score) : Condition.SortedSetScoreNotExists(key2, Score));
        var incr = tran.StringIncrementAsync(key);
        var exec = tran.ExecuteAsync();
        var get = db.StringGet(key);

        (await exec).Should().Be(expectedTranResult);
        if ((member1HasScore || member2HasScore) == demandScoreExists)
        {
            (await exec).Should().BeTrue("eq: exec");
            cond.WasSatisfied.Should().BeTrue("eq: was satisfied");
            (await incr).Should().Be(1); // eq: incr
            ((long)get).Should().Be(1); // eq: get
        }
        else
        {
            (await exec).Should().BeFalse("neq: exec");
            cond.WasSatisfied.Should().BeFalse("neq: was satisfied");
            SafeStatus(incr).Should().Be(TaskStatus.Canceled); // neq: incr
            ((long)get).Should().Be(0); // neq: get
        }
    }

    [Theory]
    [InlineData(true, true, 2L, true, true)]
    [InlineData(true, true, 2L, false, false)]
    [InlineData(true, true, 1L, true, false)]
    [InlineData(true, true, 1L, false, true)]
    [InlineData(true, false, 2L, true, false)]
    [InlineData(true, false, 2L, false, true)]
    [InlineData(true, false, 1L, true, true)]
    [InlineData(true, false, 1L, false, false)]
    [InlineData(false, true, 2L, true, false)]
    [InlineData(false, true, 2L, false, true)]
    [InlineData(false, true, 1L, true, true)]
    [InlineData(false, true, 1L, false, false)]
    [InlineData(false, false, 2L, true, false)]
    [InlineData(false, false, 2L, false, true)]
    [InlineData(false, false, 1L, true, false)]
    [InlineData(false, false, 1L, false, true)]
    public async Task basic_tran_with_sorted_set_score_count_exists_condition(bool member1HasScore, bool member2HasScore, long expectedLength, bool expectEqual, bool expectedTranResult)
    {
        await using var conn = Create();

        RedisKey key = Me(), key2 = Me() + "2";
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyDelete(key2, CommandFlags.FireAndForget);

        const double Score = 4D;
        var length = 0L;
        RedisValue member1 = "member1";
        RedisValue member2 = "member2";
        if (member1HasScore)
        {
            db.SortedSetAdd(key2, member1, Score, flags: CommandFlags.FireAndForget);
            length++;
        }

        if (member2HasScore)
        {
            db.SortedSetAdd(key2, member2, Score, flags: CommandFlags.FireAndForget);
            length++;
        }

        db.KeyExists(key).Should().BeFalse();
        db.SortedSetLength(key2, Score, Score).Should().Be(length);

        var tran = db.CreateTransaction();
        var cond = tran.AddCondition(expectEqual ? Condition.SortedSetScoreExists(key2, Score, expectedLength) : Condition.SortedSetScoreNotExists(key2, Score, expectedLength));
        var incr = tran.StringIncrementAsync(key);
        var exec = tran.ExecuteAsync();
        var get = db.StringGet(key);

        (await exec).Should().Be(expectedTranResult);
        if (expectEqual == (length == expectedLength))
        {
            (await exec).Should().BeTrue("eq: exec");
            cond.WasSatisfied.Should().BeTrue("eq: was satisfied");
            (await incr).Should().Be(1); // eq: incr
            ((long)get).Should().Be(1); // eq: get
        }
        else
        {
            (await exec).Should().BeFalse("neq: exec");
            cond.WasSatisfied.Should().BeFalse("neq: was satisfied");
            SafeStatus(incr).Should().Be(TaskStatus.Canceled); // neq: incr
            ((long)get).Should().Be(0); // neq: get
        }
    }

    [Theory]
    [InlineData("five", ComparisonType.Equal, 5L, false)]
    [InlineData("four", ComparisonType.Equal, 4L, true)]
    [InlineData("three", ComparisonType.Equal, 3L, false)]
    [InlineData("", ComparisonType.Equal, 2L, false)]
    [InlineData("", ComparisonType.Equal, 0L, true)]

    [InlineData("five", ComparisonType.LessThan, 5L, true)]
    [InlineData("four", ComparisonType.LessThan, 4L, false)]
    [InlineData("three", ComparisonType.LessThan, 3L, false)]
    [InlineData("", ComparisonType.LessThan, 2L, true)]
    [InlineData("", ComparisonType.LessThan, 0L, false)]

    [InlineData("five", ComparisonType.GreaterThan, 5L, false)]
    [InlineData("four", ComparisonType.GreaterThan, 4L, false)]
    [InlineData("three", ComparisonType.GreaterThan, 3L, true)]
    [InlineData("", ComparisonType.GreaterThan, 2L, false)]
    [InlineData("", ComparisonType.GreaterThan, 0L, false)]
    public async Task basic_tran_with_list_length_condition(string value, ComparisonType type, long length, bool expectTranResult)
    {
        await using var conn = Create();

        RedisKey key = Me(), key2 = Me() + "2";
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyDelete(key2, CommandFlags.FireAndForget);

        bool expectSuccess;
        Condition? condition;
        var valueLength = value?.Length ?? 0;
        switch (type)
        {
            case ComparisonType.Equal:
                expectSuccess = valueLength == length;
                condition = Condition.ListLengthEqual(key2, length);
                break;
            case ComparisonType.GreaterThan:
                expectSuccess = valueLength > length;
                condition = Condition.ListLengthGreaterThan(key2, length);
                break;
            case ComparisonType.LessThan:
                expectSuccess = valueLength < length;
                condition = Condition.ListLengthLessThan(key2, length);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }

        for (var i = 0; i < valueLength; i++)
        {
            db.ListRightPush(key2, i, flags: CommandFlags.FireAndForget);
        }
        db.KeyExists(key).Should().BeFalse();
        db.ListLength(key2).Should().Be(valueLength);

        var tran = db.CreateTransaction();
        var cond = tran.AddCondition(condition);
        var push = tran.StringSetAsync(key, "any value");
        var exec = tran.ExecuteAsync();
        var get = db.StringLength(key);

        (await exec).Should().Be(expectTranResult);

        if (expectSuccess)
        {
            (await exec).Should().BeTrue("eq: exec");
            cond.WasSatisfied.Should().BeTrue("eq: was satisfied");
            (await push).Should().BeTrue(); // eq: push
            get.Should().Be("any value".Length); // eq: get
        }
        else
        {
            (await exec).Should().BeFalse("neq: exec");
            cond.WasSatisfied.Should().BeFalse("neq: was satisfied");
            SafeStatus(push).Should().Be(TaskStatus.Canceled); // neq: push
            get.Should().Be(0); // neq: get
        }
    }

    [Theory]
    [InlineData("five", ComparisonType.Equal, 5L, false)]
    [InlineData("four", ComparisonType.Equal, 4L, true)]
    [InlineData("three", ComparisonType.Equal, 3L, false)]
    [InlineData("", ComparisonType.Equal, 2L, false)]
    [InlineData("", ComparisonType.Equal, 0L, true)]

    [InlineData("five", ComparisonType.LessThan, 5L, true)]
    [InlineData("four", ComparisonType.LessThan, 4L, false)]
    [InlineData("three", ComparisonType.LessThan, 3L, false)]
    [InlineData("", ComparisonType.LessThan, 2L, true)]
    [InlineData("", ComparisonType.LessThan, 0L, false)]

    [InlineData("five", ComparisonType.GreaterThan, 5L, false)]
    [InlineData("four", ComparisonType.GreaterThan, 4L, false)]
    [InlineData("three", ComparisonType.GreaterThan, 3L, true)]
    [InlineData("", ComparisonType.GreaterThan, 2L, false)]
    [InlineData("", ComparisonType.GreaterThan, 0L, false)]
    public async Task basic_tran_with_stream_length_condition(string value, ComparisonType type, long length, bool expectTranResult)
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        RedisKey key = Me(), key2 = Me() + "2";
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyDelete(key2, CommandFlags.FireAndForget);

        bool expectSuccess;
        Condition? condition;
        var valueLength = value?.Length ?? 0;
        switch (type)
        {
            case ComparisonType.Equal:
                expectSuccess = valueLength == length;
                condition = Condition.StreamLengthEqual(key2, length);
                break;
            case ComparisonType.GreaterThan:
                expectSuccess = valueLength > length;
                condition = Condition.StreamLengthGreaterThan(key2, length);
                break;
            case ComparisonType.LessThan:
                expectSuccess = valueLength < length;
                condition = Condition.StreamLengthLessThan(key2, length);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }
        RedisValue fieldName = "Test";
        for (var i = 0; i < valueLength; i++)
        {
            db.StreamAdd(key2, fieldName, i, flags: CommandFlags.FireAndForget);
        }
        db.KeyExists(key).Should().BeFalse();
        db.StreamLength(key2).Should().Be(valueLength);

        var tran = db.CreateTransaction();
        var cond = tran.AddCondition(condition);
        var push = tran.StringSetAsync(key, "any value");
        var exec = tran.ExecuteAsync();
        var get = db.StringLength(key);

        (await exec).Should().Be(expectTranResult);

        if (expectSuccess)
        {
            (await exec).Should().BeTrue("eq: exec");
            cond.WasSatisfied.Should().BeTrue("eq: was satisfied");
            (await push).Should().BeTrue(); // eq: push
            get.Should().Be("any value".Length); // eq: get
        }
        else
        {
            (await exec).Should().BeFalse("neq: exec");
            cond.WasSatisfied.Should().BeFalse("neq: was satisfied");
            SafeStatus(push).Should().Be(TaskStatus.Canceled); // neq: push
            get.Should().Be(0); // neq: get
        }
    }

    [Fact]
    public async Task basic_tran()
    {
        await using var conn = Create();

        RedisKey key = Me();
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyExists(key).Should().BeFalse();

        var tran = db.CreateTransaction();
        var a = tran.StringIncrementAsync(key, 10);
        var b = tran.StringIncrementAsync(key, 5);
        var c = tran.StringGetAsync(key);
        var d = tran.KeyExistsAsync(key);
        var e = tran.KeyDeleteAsync(key);
        var f = tran.KeyExistsAsync(key);
        a.IsCompleted.Should().BeFalse();
        b.IsCompleted.Should().BeFalse();
        c.IsCompleted.Should().BeFalse();
        d.IsCompleted.Should().BeFalse();
        e.IsCompleted.Should().BeFalse();
        f.IsCompleted.Should().BeFalse();
        var result = await tran.ExecuteAsync().ForAwait();
        result.Should().BeTrue("result");
        await Task.WhenAll(a, b, c, d, e, f).ForAwait();
        a.IsCompleted.Should().BeTrue("a");
        b.IsCompleted.Should().BeTrue("b");
        c.IsCompleted.Should().BeTrue("c");
        d.IsCompleted.Should().BeTrue("d");
        e.IsCompleted.Should().BeTrue("e");
        f.IsCompleted.Should().BeTrue("f");

        var g = db.KeyExists(key);

        (await a.ForAwait()).Should().Be(10);
        (await b.ForAwait()).Should().Be(15);
        ((long)await c.ForAwait()).Should().Be(15);
        (await d.ForAwait()).Should().BeTrue();
        (await e.ForAwait()).Should().BeTrue();
        (await f.ForAwait()).Should().BeFalse();
        g.Should().BeFalse();
    }

    [Fact]
    public async Task combine_fire_and_forget_and_regular_async_in_transaction()
    {
        await using var conn = Create();

        RedisKey key = Me();
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyExists(key).Should().BeFalse();

        var tran = db.CreateTransaction("state");
        var a = tran.StringIncrementAsync(key, 5);
        var b = tran.StringIncrementAsync(key, 10, CommandFlags.FireAndForget);
        var c = tran.StringIncrementAsync(key, 15);
        tran.Execute().Should().BeTrue();
        var count = (long)db.StringGet(key);

        (await a).Should().Be(5);
        a.AsyncState.Should().Be("state");
        (await b).Should().Be(0);
        b.AsyncState.Should().BeNull();
        (await c).Should().Be(30);
        a.AsyncState.Should().Be("state");
        count.Should().Be(30);
    }

    [Fact]
    public async Task transaction_with_ad_hoc_commands_and_select_disabled()
    {
        await using var conn = Create(disabledCommands: ["SELECT"]);
        RedisKey key = Me();
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyExists(key).Should().BeFalse();

        var tran = db.CreateTransaction("state");
        var a = tran.ExecuteAsync("SET", key, "bar");
        (await tran.ExecuteAsync()).Should().BeTrue();
        await a;
        var setting = db.StringGet(key);
        setting.Should().Be("bar");
    }

    [Fact]
    public async Task exec_completes_issue943()
    {
        Skip.UnlessLongRunning();
        int hashHit = 0, hashMiss = 0, expireHit = 0, expireMiss = 0;
        await using (var conn = Create())
        {
            var db = conn.GetDatabase();
            for (int i = 0; i < 40000; i++)
            {
                RedisKey key = Me();
                await db.KeyDeleteAsync(key);
                HashEntry[] hashEntries =
                [
                    new HashEntry("blah", DateTime.UtcNow.ToString("R")),
                ];
                ITransaction transaction = db.CreateTransaction();
                transaction.AddCondition(Condition.KeyNotExists(key));
                Task hashSetTask = transaction.HashSetAsync(key, hashEntries);
                Task<bool> expireTask = transaction.KeyExpireAsync(key, TimeSpan.FromSeconds(30));
                bool committed = await transaction.ExecuteAsync();
                if (committed)
                {
                    if (hashSetTask.IsCompleted) hashHit++; else hashMiss++;
                    if (expireTask.IsCompleted) expireHit++; else expireMiss++;
                    await hashSetTask;
                    await expireTask;
                }
            }
        }

        Log($"hash hit: {hashHit}, miss: {hashMiss}; expire hit: {expireHit}, miss: {expireMiss}");
        hashMiss.Should().Be(0);
        expireMiss.Should().Be(0);
    }

    [Fact]
    public async Task transaction_with_failing_inner_operation()
    {
        //Arrange
        RedisKey keyA = Me() + ":A", keyB = Me() + ":B", keyC = Me() + ":C";
        await using var conn = Create();
        var db = conn.GetDatabase();
        db.StringSet(keyA, "42",  flags: CommandFlags.FireAndForget);
        db.StringSet(keyB, "abc",  flags: CommandFlags.FireAndForget);
        db.StringSet(keyC, 13,  flags: CommandFlags.FireAndForget);

        var tran = db.CreateTransaction();
        var pendingA = tran.StringIncrementAsync(keyA);
        var pendingB = tran.StringIncrementAsync(keyB);

        //Act
        var pendingC = tran.StringIncrementAsync(keyC);

        //Assert
        (await tran.ExecuteAsync()).Should().BeTrue();
        (await pendingA).Should().Be(43);
        var ex = await Assert.ThrowsAsync<RedisServerException>(() => pendingB);
        ex.Message.Should().Contain("ERR value is not an integer or out of range");
        (await pendingC).Should().Be(14);
    }

    [Fact]
    public async Task transaction_with_failing_condition()
    {
        //Arrange
        RedisKey keyA = Me() + ":A", keyB = Me() + ":B", keyC = Me() + ":C";
        await using var conn = Create();
        var db = conn.GetDatabase();
        db.StringSet(keyA, "42",  flags: CommandFlags.FireAndForget);
        db.StringSet(keyB, "abc",  flags: CommandFlags.FireAndForget);
        db.StringSet(keyC, 13,  flags: CommandFlags.FireAndForget);

        var tran = db.CreateTransaction();
        var condition = tran.AddCondition(Condition.HashExists(keyA, "field"));
        var pendingA = tran.StringIncrementAsync(keyA);
        var pendingB = tran.StringIncrementAsync(keyB);

        //Act
        var pendingC = tran.StringIncrementAsync(keyC);

        //Assert
        (await tran.ExecuteAsync()).Should().BeFalse();
        condition.WasSatisfied.Should().BeFalse();
        pendingB.Status.Should().Be(TaskStatus.Canceled);
        pendingB.Status.Should().Be(TaskStatus.Canceled);
        pendingC.Status.Should().Be(TaskStatus.Canceled);
    }
}
