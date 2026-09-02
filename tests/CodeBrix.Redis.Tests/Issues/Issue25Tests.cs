using System;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.Issues; //was previously: StackExchange.Redis.Tests.Issues;

public class Issue25Tests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public void case_insensitive()
    {
        var options = ConfigurationOptions.Parse("ssl=true");
        options.Ssl.Should().BeTrue();
        options.ToString().Should().Be("ssl=True");

        options = ConfigurationOptions.Parse("SSL=TRUE");
        options.Ssl.Should().BeTrue();
        options.ToString().Should().Be("ssl=True");
    }

    [Fact]
    public void unkonwn_keyword_handling_ignore()
    {
        ConfigurationOptions.Parse("ssl2=true", true);
    }

    [Fact]
    public void unkonwn_keyword_handling_explicit_fail()
    {
        var ex = Assert.Throws<ArgumentException>(() => ConfigurationOptions.Parse("ssl2=true", false));
        ex.Message.Should().StartWith("Keyword 'ssl2' is not supported");
        ex.ParamName.Should().Be("ssl2");
    }

    [Fact]
    public void unkonwn_keyword_handling_implicit_fail()
    {
        var ex = Assert.Throws<ArgumentException>(() => ConfigurationOptions.Parse("ssl2=true"));
        ex.Message.Should().StartWith("Keyword 'ssl2' is not supported");
        ex.ParamName.Should().Be("ssl2");
    }
}
