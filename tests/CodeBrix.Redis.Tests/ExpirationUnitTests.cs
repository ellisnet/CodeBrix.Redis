using System;
using SilverAssertions;
using Xunit;
using static CodeBrix.Redis.Expiration;
namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class ExpirationUnitTests // pure tests, no DB
{
    [Fact]
    public void expire_if_not_exists_time_span_seconds()
    {
        var ex = new Expiration(TimeSpan.FromSeconds(5), ExpirationFlags.ExpireIfNotExists);
        ex.IsExpireIfNotExists.Should().BeTrue();
        ex.GetTokenCount(allowEnx: true).Should().Be(3);
        ex.ToString().Should().Be("EX 5 ENX");
    }

    [Fact]
    public void expire_if_not_exists_date_time_milliseconds()
    {
        //Arrange
        var when = new DateTime(2025, 7, 23, 10, 4, 14, DateTimeKind.Utc).AddMilliseconds(14);

        //Act
        var ex = new Expiration(when, ExpirationFlags.ExpireIfNotExists);

        //Assert
        ex.IsExpireIfNotExists.Should().BeTrue();
        ex.GetTokenCount(allowEnx: true).Should().Be(3);
        ex.ToString().Should().Be("PXAT 1753265054014 ENX");
    }

    [Fact]
    public void persist_seconds()
    {
        //Arrange
        TimeSpan? time = TimeSpan.FromMilliseconds(5000);

        //Act
        var ex = CreateOrPersist(time, false);

        //Assert
        ex.GetTokenCount(allowEnx: false).Should().Be(2);
        ex.ToString().Should().Be("EX 5");
    }

    [Fact]
    public void persist_milliseconds()
    {
        //Arrange
        TimeSpan? time = TimeSpan.FromMilliseconds(5001);

        //Act
        var ex = CreateOrPersist(time, false);

        //Assert
        ex.GetTokenCount(allowEnx: false).Should().Be(2);
        ex.ToString().Should().Be("PX 5001");
    }

    [Fact]
    public void persist_none_false()
    {
        //Arrange
        TimeSpan? time = null;

        //Act
        var ex = CreateOrPersist(time, false);

        //Assert
        ex.GetTokenCount(allowEnx: false).Should().Be(0);
        ex.ToString().Should().Be("");
    }

    [Fact]
    public void persist_none_true()
    {
        //Arrange
        TimeSpan? time = null;

        //Act
        var ex = CreateOrPersist(time, true);

        //Assert
        ex.GetTokenCount(allowEnx: false).Should().Be(1);
        ex.ToString().Should().Be("PERSIST");
    }

    [Fact]
    public void persist_both()
    {
        //Arrange
        TimeSpan? time = TimeSpan.FromMilliseconds(5000);

        //Act
        var ex = Assert.Throws<ArgumentException>(() => CreateOrPersist(time, true));

        //Assert
        ex.ParamName.Should().Be("persist");
        ex.Message.Should().StartWith("Cannot specify both expiry and persist");
    }

    [Fact]
    public void keep_ttl_seconds()
    {
        //Arrange
        TimeSpan? time = TimeSpan.FromMilliseconds(5000);

        //Act
        var ex = CreateOrKeepTtl(time, false);

        //Assert
        ex.GetTokenCount(allowEnx: false).Should().Be(2);
        ex.ToString().Should().Be("EX 5");
    }

    [Fact]
    public void keep_ttl_milliseconds()
    {
        //Arrange
        TimeSpan? time = TimeSpan.FromMilliseconds(5001);

        //Act
        var ex = CreateOrKeepTtl(time, false);

        //Assert
        ex.GetTokenCount(allowEnx: false).Should().Be(2);
        ex.ToString().Should().Be("PX 5001");
    }

    [Fact]
    public void keep_ttl_none_false()
    {
        //Arrange
        TimeSpan? time = null;

        //Act
        var ex = CreateOrKeepTtl(time, false);

        //Assert
        ex.GetTokenCount(allowEnx: false).Should().Be(0);
        ex.ToString().Should().Be("");
    }

    [Fact]
    public void keep_ttl_none_true()
    {
        //Arrange
        TimeSpan? time = null;

        //Act
        var ex = CreateOrKeepTtl(time, true);

        //Assert
        ex.GetTokenCount(allowEnx: false).Should().Be(1);
        ex.ToString().Should().Be("KEEPTTL");
    }

    [Fact]
    public void keep_ttl_both()
    {
        //Arrange
        TimeSpan? time = TimeSpan.FromMilliseconds(5000);

        //Act
        var ex = Assert.Throws<ArgumentException>(() => CreateOrKeepTtl(time, true));

        //Assert
        ex.ParamName.Should().Be("keepTtl");
        ex.Message.Should().StartWith("Cannot specify both expiry and keepTtl");
    }

    [Fact]
    public void date_time_seconds()
    {
        //Arrange
        var when = new DateTime(2025, 7, 23, 10, 4, 14, DateTimeKind.Utc);

        //Act
        var ex = new Expiration(when);

        //Assert
        ex.GetTokenCount(allowEnx: false).Should().Be(2);
        ex.ToString().Should().Be("EXAT 1753265054");
    }

    [Fact]
    public void date_time_milliseconds()
    {
        //Arrange
        var when = new DateTime(2025, 7, 23, 10, 4, 14, DateTimeKind.Utc);
        when = when.AddMilliseconds(14);

        //Act
        var ex = new Expiration(when);

        //Assert
        ex.GetTokenCount(allowEnx: false).Should().Be(2);
        ex.ToString().Should().Be("PXAT 1753265054014");
    }
}
