using System;
using System.Text;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

/// <summary>
/// Pure unit tests over the generated <see cref="RedisType"/> token parser; the tokens are the
/// literal replies from <c>TYPE</c>.
/// </summary>
public class RedisTypeMetadataUnitTests
{
    [Theory]
    [InlineData("none", RedisType.None)] // reply for a key that does not exist; see #3156
    [InlineData("string", RedisType.String)]
    [InlineData("list", RedisType.List)]
    [InlineData("set", RedisType.Set)]
    [InlineData("zset", RedisType.SortedSet)]
    [InlineData("hash", RedisType.Hash)]
    [InlineData("stream", RedisType.Stream)]
    [InlineData("vectorset", RedisType.VectorSet)]
    [InlineData("array", RedisType.Array)]
    // parsing is case-insensitive (as it was in v2, which used Enum.TryParse with ignoreCase)
    [InlineData("NONE", RedisType.None)]
    [InlineData("None", RedisType.None)]
    [InlineData("ZSet", RedisType.SortedSet)]
    [InlineData("VECTORSET", RedisType.VectorSet)]
    public void try_parse_known_tokens(string value, RedisType expected)
    {
        ReadOnlySpan<byte> bytes = Encoding.ASCII.GetBytes(value);
        RedisTypeMetadata.TryParse(bytes, out var actual).Should().BeTrue($"parse failed for '{value}'");
        actual.Should().Be(expected);
    }

    [Theory]
    // Unknown is a client-side value rather than a server token, so it is excluded from the parser
    [InlineData("unknown")]
    [InlineData("Unknown")]
    [InlineData("")]
    [InlineData("blah")]
    [InlineData("nonex")]
    public void try_parse_rejects_non_tokens(string value)
    {
        ReadOnlySpan<byte> bytes = Encoding.ASCII.GetBytes(value);
        RedisTypeMetadata.TryParse(bytes, out _).Should().BeFalse($"parse unexpectedly succeeded for '{value}'");
    }
}
