using System;
using System.Reflection;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

/// <summary>
/// Testing that things we deprecate still parse, but are otherwise defaults.
/// </summary>
public class DeprecatedTests(ITestOutputHelper output) : TestBase(output)
{
    // note: everything under test here is [Obsolete(..., error: true)], so it cannot be named directly - not
    // even via nameof, and #pragma cannot suppress an error; reflection is the only way to reach these members
    private static PropertyInfo AssertObsoleteAsError(string name)
    {
        var property = typeof(ConfigurationOptions).GetProperty(name)
            ?? throw new MissingMemberException(nameof(ConfigurationOptions), name);
        var obsolete = property.GetCustomAttribute<ObsoleteAttribute>();
        Assert.NotNull(obsolete); //kept as the xUnit form: it carries [NotNull], so the compiler's null-state flows to the dereference below
        obsolete.IsError.Should().BeTrue($"{name} should be obsolete as an error");
        return property;
    }

    private static T Get<T>(PropertyInfo property, ConfigurationOptions options) => (T)property.GetValue(options)!;

    [Fact]
    public void high_priority_socket_threads()
    {
        var property = AssertObsoleteAsError("HighPrioritySocketThreads");

        var options = ConfigurationOptions.Parse("name=Hello");
        Get<bool>(property, options).Should().BeFalse();

        options = ConfigurationOptions.Parse("highPriorityThreads=true");
        options.ToString().Should().Be("");
        Get<bool>(property, options).Should().BeFalse();

        options = ConfigurationOptions.Parse("highPriorityThreads=false");
        options.ToString().Should().Be("");
        Get<bool>(property, options).Should().BeFalse();
    }

    [Fact]
    public void preserve_async_order()
    {
        var property = AssertObsoleteAsError("PreserveAsyncOrder");

        var options = ConfigurationOptions.Parse("name=Hello");
        Get<bool>(property, options).Should().BeFalse();

        options = ConfigurationOptions.Parse("preserveAsyncOrder=true");
        options.ToString().Should().Be("");
        Get<bool>(property, options).Should().BeFalse();

        options = ConfigurationOptions.Parse("preserveAsyncOrder=false");
        options.ToString().Should().Be("");
        Get<bool>(property, options).Should().BeFalse();
    }

    [Fact]
    public void write_buffer_parse()
    {
        var property = AssertObsoleteAsError("WriteBuffer");

        var options = ConfigurationOptions.Parse("name=Hello");
        Get<int>(property, options).Should().Be(0);

        options = ConfigurationOptions.Parse("writeBuffer=8092");
        Get<int>(property, options).Should().Be(0);
    }

    [Fact]
    public void response_timeout()
    {
        var property = AssertObsoleteAsError("ResponseTimeout");

        var options = ConfigurationOptions.Parse("name=Hello");
        Get<int>(property, options).Should().Be(0);

        options = ConfigurationOptions.Parse("responseTimeout=1000");
        Get<int>(property, options).Should().Be(0);
    }
}
