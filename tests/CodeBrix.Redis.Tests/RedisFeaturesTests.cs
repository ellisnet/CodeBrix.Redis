using System;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class RedisFeaturesTests
{
    [Fact]
    public void exec_abort() // a random one because it is fun
    {
        //Arrange
        var features = new RedisFeatures(new Version(2, 9));
        var s = features.ToString();
        features.ExecAbort.Should().BeTrue();
        s.Should().StartWith("Features in 2.9" + Environment.NewLine);
        s.Should().Contain("ExecAbort: True" + Environment.NewLine);
        features = new RedisFeatures(new Version(2, 9, 5));
        s = features.ToString();
        features.ExecAbort.Should().BeFalse();
        s.Should().StartWith("Features in 2.9.5" + Environment.NewLine);
        s.Should().Contain("ExecAbort: False" + Environment.NewLine);
        features = new RedisFeatures(new Version(3, 0));

        //Act
        s = features.ToString();

        //Assert
        features.ExecAbort.Should().BeTrue();
        s.Should().StartWith("Features in 3.0" + Environment.NewLine);
        s.Should().Contain("ExecAbort: True" + Environment.NewLine);
    }
}
