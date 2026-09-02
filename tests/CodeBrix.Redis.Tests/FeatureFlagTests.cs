using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[Collection(NonParallelCollection.Name)]
public class FeatureFlagTests
{
    [Fact]
    public void unknown_flag_toggle()
    {
        ConnectionMultiplexer.GetFeatureFlag("nope").Should().BeFalse();
        ConnectionMultiplexer.SetFeatureFlag("nope", true);
        ConnectionMultiplexer.GetFeatureFlag("nope").Should().BeFalse();
    }

    [Fact]
    public void known_flag_toggle()
    {
        ConnectionMultiplexer.GetFeatureFlag("preventthreadtheft").Should().BeFalse();
        ConnectionMultiplexer.SetFeatureFlag("preventthreadtheft", true);
        ConnectionMultiplexer.GetFeatureFlag("preventthreadtheft").Should().BeTrue();
        ConnectionMultiplexer.SetFeatureFlag("preventthreadtheft", false);
        ConnectionMultiplexer.GetFeatureFlag("preventthreadtheft").Should().BeFalse();
    }
}
