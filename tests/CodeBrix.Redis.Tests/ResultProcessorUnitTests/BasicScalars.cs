using System;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

/// <summary>
/// Tests for basic scalar result processors (Int32, Int64, Double, Boolean, String, etc.)
/// </summary>
public class BasicScalars(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    [Theory]
    [InlineData(":1\r\n", 1)]
    [InlineData("+1\r\n", 1)]
    [InlineData("$1\r\n1\r\n", 1)]
    [InlineData("$?\r\n;1\r\n1\r\n;0\r\n", 1)] // streaming string
    [InlineData(",1\r\n", 1)]
    [InlineData(ATTRIB_FOO_BAR + ":1\r\n", 1)]
    [InlineData(":-42\r\n", -42)]
    [InlineData("+-42\r\n", -42)]
    [InlineData("$3\r\n-42\r\n", -42)]
    [InlineData("$?\r\n;1\r\n-\r\n;2\r\n42\r\n;0\r\n", -42)] // streaming string
    [InlineData(",-42\r\n", -42)]
    public void int32(string resp, int value) => Execute(resp, ResultProcessor.Int32).Should().Be(value);

    [Theory]
    [InlineData("+OK\r\n")]
    [InlineData("$4\r\nPONG\r\n")]
    public void failing_int32(string resp) => ExecuteUnexpected(resp, ResultProcessor.Int32);

    [Theory]
    [InlineData(":1\r\n", 1)]
    [InlineData("+1\r\n", 1)]
    [InlineData("$1\r\n1\r\n", 1)]
    [InlineData("$?\r\n;1\r\n1\r\n;0\r\n", 1)] // streaming string
    [InlineData(",1\r\n", 1)]
    [InlineData(ATTRIB_FOO_BAR + ":1\r\n", 1)]
    [InlineData(":-42\r\n", -42)]
    [InlineData("+-42\r\n", -42)]
    [InlineData("$3\r\n-42\r\n", -42)]
    [InlineData("$?\r\n;1\r\n-\r\n;2\r\n42\r\n;0\r\n", -42)] // streaming string
    [InlineData(",-42\r\n", -42)]
    public void int64(string resp, long value) => Execute(resp, ResultProcessor.Int64).Should().Be(value);

    [Theory]
    [InlineData("+OK\r\n")]
    [InlineData("$4\r\nPONG\r\n")]
    public void failing_int64(string resp) => ExecuteUnexpected(resp, ResultProcessor.Int64);

    [Theory]
    [InlineData(":42\r\n", 42.0)]
    [InlineData("+3.14\r\n", 3.14)]
    [InlineData("$4\r\n3.14\r\n", 3.14)]
    [InlineData("$?\r\n;1\r\n3\r\n;3\r\n.14\r\n;0\r\n", 3.14)] // streaming string
    [InlineData(",3.14\r\n", 3.14)]
    [InlineData(ATTRIB_FOO_BAR + ",3.14\r\n", 3.14)]
    [InlineData(":-1\r\n", -1.0)]
    [InlineData("+inf\r\n", double.PositiveInfinity)]
    [InlineData(",inf\r\n", double.PositiveInfinity)]
    [InlineData("$4\r\n-inf\r\n", double.NegativeInfinity)]
    [InlineData("$?\r\n;2\r\n-i\r\n;2\r\nnf\r\n;0\r\n", double.NegativeInfinity)] // streaming string
    [InlineData(",-inf\r\n", double.NegativeInfinity)]
    [InlineData(",nan\r\n", double.NaN)]
    //@-escaped: the snake_case of upstream's Double is the C# keyword `double`.
    public void @double(string resp, double value) => Execute(resp, ResultProcessor.Double).Should().Be(value);

    [Theory]
    [InlineData("_\r\n", null)]
    [InlineData("$-1\r\n", null)]
    [InlineData(":42\r\n", 42L)]
    [InlineData("+42\r\n", 42L)]
    [InlineData("$2\r\n42\r\n", 42L)]
    [InlineData("$?\r\n;1\r\n4\r\n;1\r\n2\r\n;0\r\n", 42L)] // streaming string
    [InlineData(",42\r\n", 42L)]
    [InlineData(ATTRIB_FOO_BAR + ":42\r\n", 42L)]
    public void nullable_int64(string resp, long? value) => Execute(resp, ResultProcessor.NullableInt64).Should().Be(value);

    [Theory]
    [InlineData("*1\r\n:99\r\n", 99L)]
    [InlineData("*?\r\n:99\r\n.\r\n", 99L)] // streaming aggregate
    [InlineData("*1\r\n$-1\r\n", null)] // unit array with RESP2 null bulk string
    [InlineData("*1\r\n_\r\n", null)] // unit array with RESP3 null
    [InlineData(ATTRIB_FOO_BAR + "*1\r\n:99\r\n", 99L)]
    public void nullable_int64_array_of_one(string resp, long? value) => Execute(resp, ResultProcessor.NullableInt64).Should().Be(value);

    [Theory]
    [InlineData("*-1\r\n")] // null array
    [InlineData("*0\r\n")] // empty array
    [InlineData("*?\r\n.\r\n")] // streaming empty aggregate
    [InlineData("*2\r\n:1\r\n:2\r\n")] // two elements
    [InlineData("*?\r\n:1\r\n:2\r\n.\r\n")] // streaming aggregate with two elements
    public void failing_nullable_int64_array_of_non_one(string resp) => ExecuteUnexpected(resp, ResultProcessor.NullableInt64);

    [Theory]
    [InlineData("_\r\n", null)]
    [InlineData("$-1\r\n", null)]
    [InlineData(":42\r\n", 42.0)]
    [InlineData("+3.14\r\n", 3.14)]
    [InlineData("$4\r\n3.14\r\n", 3.14)]
    [InlineData("$?\r\n;1\r\n3\r\n;3\r\n.14\r\n;0\r\n", 3.14)] // streaming string
    [InlineData(",3.14\r\n", 3.14)]
    [InlineData(ATTRIB_FOO_BAR + ",3.14\r\n", 3.14)]
    public void nullable_double(string resp, double? value) => Execute(resp, ResultProcessor.NullableDouble).Should().Be(value);

    [Theory]
    [InlineData("_\r\n", false)] // null = false
    [InlineData(":0\r\n", false)]
    [InlineData(":1\r\n", true)]
    [InlineData("#f\r\n", false)]
    [InlineData("#t\r\n", true)]
    [InlineData("+OK\r\n", true)]
    [InlineData(ATTRIB_FOO_BAR + ":1\r\n", true)]
    public void boolean(string resp, bool value) => Execute(resp, ResultProcessor.Boolean).Should().Be(value);

    [Theory]
    [InlineData("*1\r\n:1\r\n", true)] // SCRIPT EXISTS returns array
    [InlineData("*?\r\n:1\r\n.\r\n", true)] // streaming aggregate
    [InlineData("*1\r\n:0\r\n", false)]
    [InlineData(ATTRIB_FOO_BAR + "*1\r\n:1\r\n", true)]
    public void boolean_array_of_one(string resp, bool value) => Execute(resp, ResultProcessor.Boolean).Should().Be(value);

    [Theory]
    [InlineData("*0\r\n")] // empty array
    [InlineData("*?\r\n.\r\n")] // streaming empty aggregate
    [InlineData("*2\r\n:1\r\n:0\r\n")] // two elements
    [InlineData("*?\r\n:1\r\n:0\r\n.\r\n")] // streaming aggregate with two elements
    [InlineData("*1\r\n*1\r\n:1\r\n")] // nested array (not scalar)
    public void failing_boolean_array_of_non_one(string resp) => ExecuteUnexpected(resp, ResultProcessor.Boolean);

    [Theory]
    [InlineData("$5\r\nhello\r\n", "hello")]
    [InlineData("$?\r\n;2\r\nhe\r\n;3\r\nllo\r\n;0\r\n", "hello")] // streaming string
    [InlineData("$?\r\n;0\r\n", "")] // streaming empty string
    [InlineData("+world\r\n", "world")]
    [InlineData(":42\r\n", "42")]
    [InlineData("$-1\r\n", null)]
    [InlineData(ATTRIB_FOO_BAR + "$3\r\nfoo\r\n", "foo")]
    //@-escaped: the snake_case of upstream's String is the C# keyword `string`.
    public void @string(string resp, string? value) => Execute(resp, ResultProcessor.String).Should().Be(value);

    [Theory]
    [InlineData("*1\r\n$3\r\nbar\r\n", "bar")]
    [InlineData("*?\r\n$3\r\nbar\r\n.\r\n", "bar")] // streaming aggregate
    [InlineData(ATTRIB_FOO_BAR + "*1\r\n$3\r\nbar\r\n", "bar")]
    public void string_array_of_one(string resp, string? value) => Execute(resp, ResultProcessor.String).Should().Be(value);

    [Theory]
    [InlineData("*-1\r\n")] // null array
    [InlineData("*0\r\n")] // empty array
    [InlineData("*?\r\n.\r\n")] // streaming empty aggregate
    [InlineData("*2\r\n$3\r\nfoo\r\n$3\r\nbar\r\n")] // two elements
    [InlineData("*?\r\n$3\r\nfoo\r\n$3\r\nbar\r\n.\r\n")] // streaming aggregate with two elements
    [InlineData("*1\r\n*1\r\n$3\r\nfoo\r\n")] // nested array (not scalar)
    public void failing_string_array_of_non_one(string resp) => ExecuteUnexpected(resp, ResultProcessor.String);

    [Theory]
    [InlineData("+string\r\n", Redis.RedisType.String)]
    [InlineData("+hash\r\n", Redis.RedisType.Hash)]
    [InlineData("+zset\r\n", Redis.RedisType.SortedSet)]
    [InlineData("+set\r\n", Redis.RedisType.Set)]
    [InlineData("+list\r\n", Redis.RedisType.List)]
    [InlineData("+stream\r\n", Redis.RedisType.Stream)]
    [InlineData("+vectorset\r\n", Redis.RedisType.VectorSet)]
    [InlineData("+array\r\n", Redis.RedisType.Array)]
    [InlineData("+none\r\n", Redis.RedisType.None)] // TYPE reply for a key that does not exist; see #3156
    [InlineData("$4\r\nnone\r\n", Redis.RedisType.None)]
    [InlineData("+NONE\r\n", Redis.RedisType.None)] // parsing is case-insensitive
    [InlineData("+ZSet\r\n", Redis.RedisType.SortedSet)]
    [InlineData("+unknown\r\n", Redis.RedisType.Unknown)] // not a server token: unparsable, hence Unknown
    [InlineData("+blah\r\n", Redis.RedisType.Unknown)]
    [InlineData("$-1\r\n", Redis.RedisType.None)]
    [InlineData("_\r\n", Redis.RedisType.None)]
    [InlineData("$0\r\n\r\n", Redis.RedisType.None)]
    [InlineData(ATTRIB_FOO_BAR + "$6\r\nstring\r\n", Redis.RedisType.String)]
    public void redis_type(string resp, RedisType value) => Execute(resp, ResultProcessor.RedisType).Should().Be(value);

    [Theory]
    [InlineData("$5\r\nhello\r\n", "hello")]
    [InlineData("$?\r\n;2\r\nhe\r\n;3\r\nllo\r\n;0\r\n", "hello")] // streaming string
    [InlineData("+world\r\n", "world")]
    [InlineData(":42\r\n", "42")]
    [InlineData("$-1\r\n", null)]
    [InlineData("_\r\n", null)]
    [InlineData(ATTRIB_FOO_BAR + "$3\r\nfoo\r\n", "foo")]
    public void byte_array(string resp, string? expected)
    {
        var result = Execute(resp, ResultProcessor.ByteArray);
        if (expected is null)
        {
            result.Should().BeNull();
        }
        else
        {
            System.Text.Encoding.UTF8.GetString(result!).Should().Be(expected);
        }
    }

    [Theory]
    [InlineData("*-1\r\n")] // null array
    [InlineData("*0\r\n")] // empty array
    [InlineData("*2\r\n$3\r\nfoo\r\n$3\r\nbar\r\n")] // array
    public void failing_byte_array(string resp) => ExecuteUnexpected(resp, ResultProcessor.ByteArray);

    [Theory]
    [InlineData("$5\r\nhello\r\n", "hello")]
    [InlineData("$?\r\n;2\r\nhe\r\n;3\r\nllo\r\n;0\r\n", "hello")] // streaming string
    [InlineData("+world\r\n", "world")]
    [InlineData("$-1\r\n", null)]
    [InlineData("_\r\n", null)]
    [InlineData(ATTRIB_FOO_BAR + "$11\r\nclusterinfo\r\n", "clusterinfo")]
    // note that this test does not include a valid cluster nodes response
    public void cluster_nodes_raw(string resp, string? expected) => Execute(resp, ResultProcessor.ClusterNodesRaw).Should().Be(expected);

    [Theory]
    [InlineData("*0\r\n")] // empty array
    [InlineData("*2\r\n$3\r\nfoo\r\n$3\r\nbar\r\n")] // array
    public void failing_cluster_nodes_raw(string resp) => ExecuteUnexpected(resp, ResultProcessor.ClusterNodesRaw);

    [Theory]
    [InlineData(":42\r\n", 42L)]
    [InlineData("+99\r\n", 99L)]
    [InlineData("$2\r\n10\r\n", 10L)]
    [InlineData("$?\r\n;1\r\n1\r\n;1\r\n0\r\n;0\r\n", 10L)] // streaming string
    [InlineData(",123\r\n", 123L)]
    [InlineData(ATTRIB_FOO_BAR + ":42\r\n", 42L)]
    public void int64_default_value(string resp, long expected) => Execute(resp, Int64DefaultValue999).Should().Be(expected);

    [Theory]
    [InlineData("_\r\n", 999L)] // null returns default
    [InlineData("$-1\r\n", 999L)] // null returns default
    public void int64_default_value_null(string resp, long expected) => Execute(resp, Int64DefaultValue999).Should().Be(expected);

    [Theory]
    [InlineData("*0\r\n")] // empty array
    [InlineData("*2\r\n:1\r\n:2\r\n")] // array
    [InlineData("+notanumber\r\n")] // invalid number
    public void failing_int64_default_value(string resp) => ExecuteUnexpected(resp, Int64DefaultValue999);

    [Theory]
    [InlineData("$5\r\nhello\r\n", "hello")]
    [InlineData("$?\r\n;2\r\nhe\r\n;3\r\nllo\r\n;0\r\n", "hello")] // streaming string
    [InlineData("+world\r\n", "world")]
    [InlineData(":42\r\n", "42")]
    [InlineData("$-1\r\n", "(null)")]
    [InlineData("_\r\n", "(null)")]
    [InlineData(ATTRIB_FOO_BAR + "$3\r\nfoo\r\n", "foo")]
    public void redis_key(string resp, string expected)
    {
        //Act
        var result = Execute(resp, ResultProcessor.RedisKey);

        //Assert
        result.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData("*0\r\n")] // empty array
    [InlineData("*2\r\n$3\r\nfoo\r\n$3\r\nbar\r\n")] // array
    public void failing_redis_key(string resp) => ExecuteUnexpected(resp, ResultProcessor.RedisKey);

    [Theory]
    [InlineData("$5\r\nhello\r\n", "hello")]
    [InlineData("$?\r\n;2\r\nhe\r\n;3\r\nllo\r\n;0\r\n", "hello")] // streaming string
    [InlineData("+world\r\n", "world")]
    [InlineData(":42\r\n", "42")]
    [InlineData("$-1\r\n", "")]
    [InlineData("_\r\n", "")]
    [InlineData(",3.14\r\n", "3.14")]
    [InlineData(ATTRIB_FOO_BAR + "$3\r\nfoo\r\n", "foo")]
    public void redis_value(string resp, string expected)
    {
        //Act
        var result = Execute(resp, ResultProcessor.RedisValue);

        //Assert
        result.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData("*0\r\n")] // empty array
    [InlineData("*2\r\n$3\r\nfoo\r\n$3\r\nbar\r\n")] // array
    public void failing_redis_value(string resp) => ExecuteUnexpected(resp, ResultProcessor.RedisValue);

    [Theory]
    [InlineData("$5\r\nhello\r\n", "hello")]
    [InlineData("$?\r\n;2\r\nhe\r\n;3\r\nllo\r\n;0\r\n", "hello")] // streaming string
    [InlineData("+world\r\n", "world")]
    [InlineData("$-1\r\n", null)]
    [InlineData("_\r\n", null)]
    [InlineData(ATTRIB_FOO_BAR + "$10\r\ntiebreaker\r\n", "tiebreaker")]
    public void tie_breaker(string resp, string? expected) => Execute(resp, ResultProcessor.TieBreaker).Should().Be(expected);

    [Theory]
    [InlineData("*0\r\n")] // empty array
    [InlineData("*2\r\n$3\r\nfoo\r\n$3\r\nbar\r\n")] // array
    public void failing_tie_breaker(string resp) => ExecuteUnexpected(resp, ResultProcessor.TieBreaker);
}
