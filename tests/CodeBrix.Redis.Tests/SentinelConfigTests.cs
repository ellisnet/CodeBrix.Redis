using System;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class SentinelConfigTests
{
    [Fact]
    public void parse_sentinel_credentials_from_connection_string()
    {
        //Arrange
        var cs = "localhost:26379,serviceName=myprimary,sentinelUser=su,sentinelPassword=sp";

        //Act
        var options = ConfigurationOptions.Parse(cs);

        //Assert
        options.SentinelUser.Should().Be("su");
        options.SentinelPassword.Should().Be("sp");
        options.ServiceName.Should().Be("myprimary");
    }

    [Fact]
    public void to_string_masks_sentinel_password_when_excluded()
    {
        //Arrange
        var options = new ConfigurationOptions();
        options.EndPoints.Add("localhost", 26379);
        options.ServiceName = "myprimary";
        options.SentinelUser = "su";
        options.SentinelPassword = "secret";

        //Act
        var repr = options.ToString(includePassword: false);

        //Assert
        repr.Should().Contain("sentinelUser=su");
        repr.Should().Contain("sentinelPassword=*****");
        repr.Should().NotContain("secret");
    }

    [Fact]
    public void clone_preserves_sentinel_credentials()
    {
        //Arrange
        var options = new ConfigurationOptions();
        options.SentinelUser = "su";
        options.SentinelPassword = "sp";

        //Act
        var clone = options.Clone();

        //Assert
        clone.SentinelUser.Should().Be(options.SentinelUser);
        clone.SentinelPassword.Should().Be(options.SentinelPassword);
    }
}
