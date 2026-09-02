using System;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class AutoConfigureInfoFieldUnitTests
{
    [Theory]
    [InlineData("role", (int)AutoConfigureInfoField.Role)]
    [InlineData("master_host", (int)AutoConfigureInfoField.MasterHost)]
    [InlineData("master_port", (int)AutoConfigureInfoField.MasterPort)]
    [InlineData("redis_version", (int)AutoConfigureInfoField.RedisVersion)]
    [InlineData("redis_mode", (int)AutoConfigureInfoField.RedisMode)]
    [InlineData("run_id", (int)AutoConfigureInfoField.RunId)]
    [InlineData("garnet_version", (int)AutoConfigureInfoField.GarnetVersion)]
    [InlineData("valkey_version", (int)AutoConfigureInfoField.ValkeyVersion)]
    [InlineData("server_mode", (int)AutoConfigureInfoField.ServerMode)]
    public void try_parse_char_span_known_fields(string value, int expected)
    {
        AutoConfigureInfoFieldMetadata.TryParse(value.AsSpan(), out var actual).Should().BeTrue();
        ((int)actual).Should().Be(expected);
    }

    [Fact]
    public void try_parse_char_span_unknown_field() => AutoConfigureInfoFieldMetadata.TryParse("server_name".AsSpan(), out _).Should().BeFalse();
}
