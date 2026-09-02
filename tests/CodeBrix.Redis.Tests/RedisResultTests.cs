using System;
using System.Collections.Generic;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

/// <summary>
/// Tests for <see cref="RedisResult"/>.
/// </summary>
public sealed class RedisResultTests
{
    /// <summary>
    /// Tests the basic functionality of <see cref="RedisResult.ToDictionary(IEqualityComparer{string})"/>.
    /// </summary>
    [Fact]
    public void to_dictionary_works()
    {
        //Arrange
        var redisArrayResult = RedisResult.Create(
            ["one", 1, "two", 2, "three", 3, "four", 4]);

        //Act
        var dict = redisArrayResult.ToDictionary();

        //Assert
        dict.Count.Should().Be(4);
        ((RedisValue)dict["one"]).Should().Be(1);
        ((RedisValue)dict["two"]).Should().Be(2);
        ((RedisValue)dict["three"]).Should().Be(3);
        ((RedisValue)dict["four"]).Should().Be(4);
    }

    /// <summary>
    /// Tests the basic functionality of <see cref="RedisResult.ToDictionary(IEqualityComparer{string})"/>
    /// when the results contain a nested results array, which is common for lua script results.
    /// </summary>
    [Fact]
    public void to_dictionary_works_when_nested()
    {
        //Arrange
        var redisArrayResult = RedisResult.Create(
            [
                RedisResult.Create((RedisValue)"one"),
                RedisResult.Create(["two", 2, "three", 3]),

                RedisResult.Create((RedisValue)"four"),
                RedisResult.Create(["five", 5, "six", 6]),
            ]);
        var dict = redisArrayResult.ToDictionary();

        //Act
        var nestedDict = dict["one"].ToDictionary();

        //Assert
        dict.Count.Should().Be(2);
        nestedDict.Count.Should().Be(2);
        ((RedisValue)nestedDict["two"]).Should().Be(2);
        ((RedisValue)nestedDict["three"]).Should().Be(3);
    }

    /// <summary>
    /// Tests that <see cref="RedisResult.ToDictionary(IEqualityComparer{string})"/> fails when a duplicate key is encountered.
    /// This also tests that the default comparator is case-insensitive.
    /// </summary>
    [Fact]
    public void to_dictionary_fails_with_duplicate_keys()
    {
        var redisArrayResult = RedisResult.Create(
            ["banana", 1, "BANANA", 2, "orange", 3, "apple", 4]);

        Assert.Throws<ArgumentException>(() => redisArrayResult.ToDictionary(/* Use default comparer, causes collision of banana */));
    }

    /// <summary>
    /// Tests that <see cref="RedisResult.ToDictionary(IEqualityComparer{string})"/> correctly uses the provided comparator.
    /// </summary>
    [Fact]
    public void to_dictionary_works_with_custom_comparator()
    {
        //Arrange
        var redisArrayResult = RedisResult.Create(
            ["banana", 1, "BANANA", 2, "orange", 3, "apple", 4]);

        //Act
        var dict = redisArrayResult.ToDictionary(StringComparer.Ordinal);

        //Assert
        dict.Count.Should().Be(4);
        ((RedisValue)dict["banana"]).Should().Be(1);
        ((RedisValue)dict["BANANA"]).Should().Be(2);
    }

    /// <summary>
    /// Tests that <see cref="RedisResult.ToDictionary(IEqualityComparer{string})"/> fails when the redis results array contains an odd number
    /// of elements.  In other words, it's not actually a Key,Value,Key,Value... etc. array.
    /// </summary>
    [Fact]
    public void to_dictionary_fails_on_mishapen_results()
    {
        var redisArrayResult = RedisResult.Create(
            ["one", 1, "two", 2, "three", 3, "four" /* missing 4 */]);

        Assert.Throws<IndexOutOfRangeException>(() => redisArrayResult.ToDictionary(StringComparer.Ordinal));
    }

    [Fact]
    public void single_result_convertible_via_to()
    {
        var value = RedisResult.Create(123);
        Assert.StrictEqual((int)123, Convert.ToInt32(value));
        Assert.StrictEqual((uint)123U, Convert.ToUInt32(value));
        Assert.StrictEqual(123L, Convert.ToInt64(value));
        Assert.StrictEqual(123UL, Convert.ToUInt64(value));
        Assert.StrictEqual((byte)123, Convert.ToByte(value));
        Assert.StrictEqual((sbyte)123, Convert.ToSByte(value));
        Assert.StrictEqual((short)123, Convert.ToInt16(value));
        Assert.StrictEqual((ushort)123, Convert.ToUInt16(value));
        Convert.ToString(value).Should().Be("123");
        Assert.StrictEqual(123M, Convert.ToDecimal(value));
        Assert.StrictEqual((char)123, Convert.ToChar(value));
        Assert.StrictEqual(123f, Convert.ToSingle(value));
        Assert.StrictEqual(123d, Convert.ToDouble(value));
    }

    [Fact]
    public void single_result_convertible_direct_via_change_type_type()
    {
        var value = RedisResult.Create(123);
        Assert.StrictEqual((int)123, Convert.ChangeType(value, typeof(int)));
        Assert.StrictEqual((uint)123U, Convert.ChangeType(value, typeof(uint)));
        Assert.StrictEqual(123L, Convert.ChangeType(value, typeof(long)));
        Assert.StrictEqual(123UL, Convert.ChangeType(value, typeof(ulong)));
        Assert.StrictEqual((byte)123, Convert.ChangeType(value, typeof(byte)));
        Assert.StrictEqual((sbyte)123, Convert.ChangeType(value, typeof(sbyte)));
        Assert.StrictEqual((short)123, Convert.ChangeType(value, typeof(short)));
        Assert.StrictEqual((ushort)123, Convert.ChangeType(value, typeof(ushort)));
        Convert.ChangeType(value, typeof(string)).Should().Be("123");
        Assert.StrictEqual(123M, Convert.ChangeType(value, typeof(decimal)));
        Assert.StrictEqual((char)123, Convert.ChangeType(value, typeof(char)));
        Assert.StrictEqual(123f, Convert.ChangeType(value, typeof(float)));
        Assert.StrictEqual(123d, Convert.ChangeType(value, typeof(double)));
    }

    [Fact]
    public void single_result_convertible_direct_via_change_type_type_code()
    {
        var value = RedisResult.Create(123);
        Assert.StrictEqual((int)123, Convert.ChangeType(value, TypeCode.Int32));
        Assert.StrictEqual((uint)123U, Convert.ChangeType(value, TypeCode.UInt32));
        Assert.StrictEqual(123L, Convert.ChangeType(value, TypeCode.Int64));
        Assert.StrictEqual(123UL, Convert.ChangeType(value, TypeCode.UInt64));
        Assert.StrictEqual((byte)123, Convert.ChangeType(value, TypeCode.Byte));
        Assert.StrictEqual((sbyte)123, Convert.ChangeType(value, TypeCode.SByte));
        Assert.StrictEqual((short)123, Convert.ChangeType(value, TypeCode.Int16));
        Assert.StrictEqual((ushort)123, Convert.ChangeType(value, TypeCode.UInt16));
        Convert.ChangeType(value, TypeCode.String).Should().Be("123");
        Assert.StrictEqual(123M, Convert.ChangeType(value, TypeCode.Decimal));
        Assert.StrictEqual((char)123, Convert.ChangeType(value, TypeCode.Char));
        Assert.StrictEqual(123f, Convert.ChangeType(value, TypeCode.Single));
        Assert.StrictEqual(123d, Convert.ChangeType(value, TypeCode.Double));
    }

    [Theory]
    [InlineData(ResultType.Double)]
    [InlineData(ResultType.BulkString)]
    [InlineData(ResultType.SimpleString)]
    public void redis_result_parse_na_n(ResultType resultType)
    {
        // https://github.com/redis/NRedisStack/issues/439
        var value = RedisResult.Create("NaN", resultType);
        double.IsNaN(value.AsDouble()).Should().BeTrue();
    }

    [Theory]
    [InlineData(ResultType.Double)]
    [InlineData(ResultType.BulkString)]
    [InlineData(ResultType.SimpleString)]
    public void redis_result_parse_inf(ResultType resultType)
    {
        // https://github.com/redis/NRedisStack/issues/439
        var value = RedisResult.Create("inf", resultType);
        double.IsPositiveInfinity(value.AsDouble()).Should().BeTrue();
    }

    [Theory]
    [InlineData(ResultType.Double)]
    [InlineData(ResultType.BulkString)]
    [InlineData(ResultType.SimpleString)]
    public void redis_result_parse_plus_inf(ResultType resultType)
    {
        // https://github.com/redis/NRedisStack/issues/439
        var value = RedisResult.Create("+inf", resultType);
        double.IsPositiveInfinity(value.AsDouble()).Should().BeTrue();
    }

    [Theory]
    [InlineData(ResultType.Double)]
    [InlineData(ResultType.BulkString)]
    [InlineData(ResultType.SimpleString)]
    public void redis_result_parse_minus_inf(ResultType resultType)
    {
        // https://github.com/redis/NRedisStack/issues/439
        var value = RedisResult.Create("-inf", resultType);
        double.IsNegativeInfinity(value.AsDouble()).Should().BeTrue();
    }
}
