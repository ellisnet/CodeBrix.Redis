using System;
using System.Text;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class ChannelTests
{
    [Fact]
    public void use_implicit_auto_pattern_on_by_default() => RedisChannel.UseImplicitAutoPattern.Should().BeTrue();

    [Theory]
    [InlineData("abc", true, false)]
    [InlineData("abc*def", true, true)]
    [InlineData("abc", false, false)]
    [InlineData("abc*def", false, false)]
    //[Obsolete] on the TEST, not a suppression: the implicit string/byte[] -> RedisChannel
    //conversion is itself [Obsolete] and is exactly what this test exists to exercise (it asserts
    //what UseImplicitAutoPattern does to that conversion). C# does not report CS0618 inside a
    //member that is itself obsolete, which is the language's own opt-in for this case.
    [Obsolete("Exercises the [Obsolete] implicit string -> RedisChannel conversion, deliberately")]
    public void validate_auto_pattern_mode_string(string name, bool useImplicitAutoPattern, bool isPatternBased)
    {
        bool oldValue = RedisChannel.UseImplicitAutoPattern;
        try
        {
            RedisChannel.UseImplicitAutoPattern = useImplicitAutoPattern;
            RedisChannel channel = name;
            channel.IsPattern.Should().Be(isPatternBased);
        }
        finally
        {
            RedisChannel.UseImplicitAutoPattern = oldValue;
        }
    }

    [Theory]
    [InlineData("abc", RedisChannel.PatternMode.Auto, true, false)]
    [InlineData("abc*def", RedisChannel.PatternMode.Auto, true, true)]
    [InlineData("abc", RedisChannel.PatternMode.Literal, true, false)]
    [InlineData("abc*def", RedisChannel.PatternMode.Literal, true, false)]
    [InlineData("abc", RedisChannel.PatternMode.Pattern, true, true)]
    [InlineData("abc*def", RedisChannel.PatternMode.Pattern, true, true)]
    [InlineData("abc", RedisChannel.PatternMode.Auto, false, false)]
    [InlineData("abc*def", RedisChannel.PatternMode.Auto, false, true)]
    [InlineData("abc", RedisChannel.PatternMode.Literal, false, false)]
    [InlineData("abc*def", RedisChannel.PatternMode.Literal, false, false)]
    [InlineData("abc", RedisChannel.PatternMode.Pattern, false, true)]
    [InlineData("abc*def", RedisChannel.PatternMode.Pattern, false, true)]
    public void validate_mode_specified_ignores_global_setting(string name, RedisChannel.PatternMode mode, bool useImplicitAutoPattern, bool isPatternBased)
    {
        bool oldValue = RedisChannel.UseImplicitAutoPattern;
        try
        {
            RedisChannel.UseImplicitAutoPattern = useImplicitAutoPattern;
            RedisChannel channel = new(name, mode);
            channel.IsPattern.Should().Be(isPatternBased);
        }
        finally
        {
            RedisChannel.UseImplicitAutoPattern = oldValue;
        }
    }

    [Theory]
    [InlineData("abc", true, false)]
    [InlineData("abc*def", true, true)]
    [InlineData("abc", false, false)]
    [InlineData("abc*def", false, false)]
    //[Obsolete] on the TEST, not a suppression - see validate_auto_pattern_mode_string above.
    [Obsolete("Exercises the [Obsolete] implicit byte[] -> RedisChannel conversion, deliberately")]
    public void validate_auto_pattern_mode_bytes(string name, bool useImplicitAutoPattern, bool isPatternBased)
    {
        var bytes = Encoding.UTF8.GetBytes(name);
        bool oldValue = RedisChannel.UseImplicitAutoPattern;
        try
        {
            RedisChannel.UseImplicitAutoPattern = useImplicitAutoPattern;
            RedisChannel channel = bytes;
            channel.IsPattern.Should().Be(isPatternBased);
        }
        finally
        {
            RedisChannel.UseImplicitAutoPattern = oldValue;
        }
    }

    [Theory]
    [InlineData("abc", RedisChannel.PatternMode.Auto, true, false)]
    [InlineData("abc*def", RedisChannel.PatternMode.Auto, true, true)]
    [InlineData("abc", RedisChannel.PatternMode.Literal, true, false)]
    [InlineData("abc*def", RedisChannel.PatternMode.Literal, true, false)]
    [InlineData("abc", RedisChannel.PatternMode.Pattern, true, true)]
    [InlineData("abc*def", RedisChannel.PatternMode.Pattern, true, true)]
    [InlineData("abc", RedisChannel.PatternMode.Auto, false, false)]
    [InlineData("abc*def", RedisChannel.PatternMode.Auto, false, true)]
    [InlineData("abc", RedisChannel.PatternMode.Literal, false, false)]
    [InlineData("abc*def", RedisChannel.PatternMode.Literal, false, false)]
    [InlineData("abc", RedisChannel.PatternMode.Pattern, false, true)]
    [InlineData("abc*def", RedisChannel.PatternMode.Pattern, false, true)]
    public void validate_mode_specified_ignores_global_setting_bytes(string name, RedisChannel.PatternMode mode, bool useImplicitAutoPattern, bool isPatternBased)
    {
        var bytes = Encoding.UTF8.GetBytes(name);
        bool oldValue = RedisChannel.UseImplicitAutoPattern;
        try
        {
            RedisChannel.UseImplicitAutoPattern = useImplicitAutoPattern;
            RedisChannel channel = new(bytes, mode);
            channel.IsPattern.Should().Be(isPatternBased);
        }
        finally
        {
            RedisChannel.UseImplicitAutoPattern = oldValue;
        }
    }

    [Theory]
    [InlineData("abc*def", false)]
    [InlineData("abcdef", false)]
    [InlineData("abc*def", true)]
    [InlineData("abcdef", true)]
    public void validate_literal_pattern_mode(string name, bool useImplicitAutoPattern)
    {
        bool oldValue = RedisChannel.UseImplicitAutoPattern;
        try
        {
            RedisChannel.UseImplicitAutoPattern = useImplicitAutoPattern;
            RedisChannel channel;

            // literal, string
            channel = RedisChannel.Literal(name);
            channel.IsPattern.Should().BeFalse();

            // pattern, string
            channel = RedisChannel.Pattern(name);
            channel.IsPattern.Should().BeTrue();

            var bytes = Encoding.UTF8.GetBytes(name);

            // literal, byte[]
            channel = RedisChannel.Literal(bytes);
            channel.IsPattern.Should().BeFalse();

            // pattern, byte[]
            channel = RedisChannel.Pattern(bytes);
            channel.IsPattern.Should().BeTrue();
        }
        finally
        {
            RedisChannel.UseImplicitAutoPattern = oldValue;
        }
    }
}
