using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Text;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class DigestUnitTests(ITestOutputHelper output) : TestBase(output)
{
    [Theory]
    [MemberData(nameof(SimpleDigestTestValues))]
    public void redis_value_digest(string equivalentValue, RedisValue value)
    {
        // first, use pure XxHash3 to see what we expect
        var hashHex = GetXxh3Hex(equivalentValue);

        var digest = value.Digest();
        digest.Kind.Should().Be(ValueCondition.ConditionKind.DigestEquals);

        digest.ToString().Should().Be($"IFDEQ {hashHex}");
    }

    public static IEnumerable<object[]> SimpleDigestTestValues()
    {
        yield return ["Hello World", (RedisValue)"Hello World"];
        yield return ["42", (RedisValue)"42"];
        yield return ["42", (RedisValue)42];
    }

    [Theory]
    [InlineData("Hello World", "e34615aade2e6333")]
    [InlineData("42", "1217cb28c0ef2191")]
    public void value_condition_calculate_digest(string source, string expected)
    {
        var digest = ValueCondition.CalculateDigest(Encoding.UTF8.GetBytes(source));
        digest.ToString().Should().Be($"IFDEQ {expected}");
    }

    [Theory]
    [InlineData("e34615aade2e6333")]
    [InlineData("1217cb28c0ef2191")]
    public void value_condition_parse_digest(string value)
    {
        // parse from hex chars
        var digest = ValueCondition.ParseDigest(value.AsSpan());
        digest.ToString().Should().Be($"IFDEQ {value}");

        // and the same, from hex bytes
        digest = ValueCondition.ParseDigest(Encoding.UTF8.GetBytes(value).AsSpan());
        digest.ToString().Should().Be($"IFDEQ {value}");
    }

    [Theory]
    [InlineData("Hello World", "e34615aade2e6333")]
    [InlineData("42", "1217cb28c0ef2191")]
    [InlineData("", "2d06800538d394c2")]
    [InlineData("a", "e6c632b61e964e1f")]
    public void known_xxh3_values(string source, string expected)
        => GetXxh3Hex(source).Should().Be(expected);

    private static string GetXxh3Hex(string source)
    {
        var len = Encoding.UTF8.GetMaxByteCount(source.Length);
        var oversized = ArrayPool<byte>.Shared.Rent(len);
        var bytes = Encoding.UTF8.GetBytes(source, oversized);
        var result = GetXxh3Hex(oversized.AsSpan(0, bytes));
        ArrayPool<byte>.Shared.Return(oversized);
        return result;
    }

    private static string GetXxh3Hex(ReadOnlySpan<byte> source)
    {
        byte[] targetBytes = new byte[8];
        XxHash3.Hash(source, targetBytes);
        return BitConverter.ToString(targetBytes).Replace("-", string.Empty).ToLowerInvariant();
    }

    [Fact]
    public void value_condition_mutations()
    {
        const string InputValue =
            "Meantime we shall express our darker purpose.\nGive me the map there. Know we have divided\nIn three our kingdom; and 'tis our fast intent\nTo shake all cares and business from our age,\nConferring them on younger strengths while we\nUnburthen'd crawl toward death. Our son of Cornwall,\nAnd you, our no less loving son of Albany,\nWe have this hour a constant will to publish\nOur daughters' several dowers, that future strife\nMay be prevented now. The princes, France and Burgundy,\nGreat rivals in our youngest daughter's love,\nLong in our court have made their amorous sojourn,\nAnd here are to be answer'd.";

        var condition = ValueCondition.Equal(InputValue);
        condition.ToString().Should().Be($"IFEQ {InputValue}");
        condition.IsValueTest.Should().BeTrue();
        condition.IsDigestTest.Should().BeFalse();
        condition.IsNegated.Should().BeFalse();
        condition.IsExistenceTest.Should().BeFalse();

        var negCondition = !condition;
        negCondition.Should().NotBe(condition);
        negCondition.ToString().Should().Be($"IFNE {InputValue}");
        negCondition.IsValueTest.Should().BeTrue();
        negCondition.IsDigestTest.Should().BeFalse();
        negCondition.IsNegated.Should().BeTrue();
        negCondition.IsExistenceTest.Should().BeFalse();

        var negNegCondition = !negCondition;
        negNegCondition.Should().Be(condition);

        var digest = condition.AsDigest();
        digest.Should().NotBe(condition);
        digest.ToString().Should().Be($"IFDEQ {GetXxh3Hex(InputValue)}");
        digest.IsValueTest.Should().BeFalse();
        digest.IsDigestTest.Should().BeTrue();
        digest.IsNegated.Should().BeFalse();
        digest.IsExistenceTest.Should().BeFalse();

        var negDigest = !digest;
        negDigest.Should().NotBe(digest);
        negDigest.ToString().Should().Be($"IFDNE {GetXxh3Hex(InputValue)}");
        negDigest.IsValueTest.Should().BeFalse();
        negDigest.IsDigestTest.Should().BeTrue();
        negDigest.IsNegated.Should().BeTrue();
        negDigest.IsExistenceTest.Should().BeFalse();

        var negNegDigest = !negDigest;
        negNegDigest.Should().Be(digest);

        var @default = default(ValueCondition);
        @default.IsValueTest.Should().BeFalse();
        @default.IsDigestTest.Should().BeFalse();
        @default.IsNegated.Should().BeFalse();
        @default.IsExistenceTest.Should().BeFalse();
        @default.ToString().Should().Be("");
        @default.Should().Be(ValueCondition.Always);

        var ex = Assert.Throws<InvalidOperationException>(() => !@default);
        ex.Message.Should().Be("operator ! cannot be used with a Always condition.");

        var exists = ValueCondition.Exists;
        exists.IsValueTest.Should().BeFalse();
        exists.IsDigestTest.Should().BeFalse();
        exists.IsNegated.Should().BeFalse();
        exists.IsExistenceTest.Should().BeTrue();
        exists.ToString().Should().Be("XX");

        var notExists = ValueCondition.NotExists;
        notExists.IsValueTest.Should().BeFalse();
        notExists.IsDigestTest.Should().BeFalse();
        notExists.IsNegated.Should().BeTrue();
        notExists.IsExistenceTest.Should().BeTrue();
        notExists.ToString().Should().Be("NX");

        notExists.Should().NotBe(exists);
        (!notExists).Should().Be(exists);
        (!exists).Should().Be(notExists);
    }

    [Fact]
    public void random_bytes()
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(8000);
        var rand = new Random();

        for (int i = 0; i < 100; i++)
        {
            var len = rand.Next(1, buffer.Length);
            var span = buffer.AsSpan(0, len);
            rand.NextBytes(span);
            var digest = ValueCondition.CalculateDigest(span);
            digest.ToString().Should().Be($"IFDEQ {GetXxh3Hex(span)}");
        }
    }
}
