using System.IO;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.Issues; //was previously: StackExchange.Redis.Tests.Issues;

public class DefaultDatabaseTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public void unspecified_db_id_returns_null()
    {
        var config = ConfigurationOptions.Parse("localhost");
        config.DefaultDatabase.Should().BeNull();
    }

    [Fact]
    public void specified_db_id_returns_expected()
    {
        var config = ConfigurationOptions.Parse("localhost,defaultDatabase=3");
        config.DefaultDatabase.Should().Be(3);
    }

    [Fact]
    public async Task configuration_options_unspecified_default_db()
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        var log = new StringWriter();
        try
        {
            await using var conn = await ConnectionMultiplexer.ConnectAsync(TestConfig.Current.PrimaryServerAndPort, log);
            var db = conn.GetDatabase();
            db.Database.Should().Be(0);
        }
        finally
        {
            Log(log.ToString());
        }
    }

    [Fact]
    public async Task configuration_options_specified_default_db()
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        var log = new StringWriter();
        try
        {
            await using var conn = await ConnectionMultiplexer.ConnectAsync($"{TestConfig.Current.PrimaryServerAndPort},defaultDatabase=3", log);
            var db = conn.GetDatabase();
            db.Database.Should().Be(3);
        }
        finally
        {
            Log(log.ToString());
        }
    }
}
