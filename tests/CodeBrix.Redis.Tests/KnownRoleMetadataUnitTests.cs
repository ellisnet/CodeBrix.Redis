using System;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class KnownRoleMetadataUnitTests
{
    [Theory]
    [InlineData("primary", false)]
    [InlineData("master", false)]
    [InlineData("replica", true)]
    [InlineData("slave", true)]
    public void try_parse_char_span_known_roles(string value, bool expected)
    {
        KnownRoleMetadata.TryParse(value.AsSpan(), out var actual).Should().BeTrue();
        actual.Should().Be(expected);
    }

    [Fact]
    public void try_parse_char_span_unknown_role() => KnownRoleMetadata.TryParse("sentinel".AsSpan(), out _).Should().BeFalse();
}
