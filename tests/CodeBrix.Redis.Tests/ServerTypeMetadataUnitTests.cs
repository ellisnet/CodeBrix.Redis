using System;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class ServerTypeMetadataUnitTests
{
    [Theory]
    [InlineData("standalone", (int)ServerType.Standalone)]
    [InlineData("cluster", (int)ServerType.Cluster)]
    [InlineData("sentinel", (int)ServerType.Sentinel)]
    public void try_parse_char_span_known_server_types(string value, int expected)
    {
        ServerTypeMetadata.TryParse(value.AsSpan(), out var actual).Should().BeTrue();
        ((int)actual).Should().Be(expected);
    }

    [Theory]
    [InlineData("twemproxy")]
    [InlineData("envoyproxy")]
    public void try_parse_char_span_ignores_non_auto_configured_types(string value) => ServerTypeMetadata.TryParse(value.AsSpan(), out _).Should().BeFalse();
}
