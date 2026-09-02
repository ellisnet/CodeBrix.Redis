using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.Issues; //was previously: StackExchange.Redis.Tests.Issues;

public class Issue2653
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("abcdef", "abcdef")]
    [InlineData("abc.def", "abc.def")]
    [InlineData("abc d \t  ef", "abc-d-ef")]
    [InlineData("  abc\r\ndef\n", "abc-def")]
    public void check_libray_sanitization(string? input, string expected)
        => ServerEndPoint.ClientInfoSanitize(input).Should().Be(expected);
}
