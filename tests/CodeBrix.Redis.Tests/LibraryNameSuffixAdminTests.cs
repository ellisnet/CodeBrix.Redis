using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class LibraryNameSuffixAdminTests(InProcServerFixture fixture)
{
    private ConfigurationOptions NoAdminConfig()
    {
        // start from the shared in-process server config, but explicitly *disable* admin mode
        var options = fixture.Config.Clone();
        options.AllowAdmin = false;
        options.AllowAdmin.Should().BeFalse();
        return options;
    }

    [Fact]
    public async Task add_library_name_suffix_works_without_admin()
    {
        await using var conn = await ConnectionMultiplexer.ConnectAsync(NoAdminConfig());

        // internally this fixes up connected servers via CLIENT SETINFO (best-effort); the
        // CLIENT SETINFO sub-command is not admin; don't report it (telemetry, etc)
        conn.AddLibraryNameSuffix("mysuffix");
    }

    [Fact]
    public async Task client_sub_commands_via_execute_do_not_require_admin()
    {
        await using var conn = await ConnectionMultiplexer.ConnectAsync(NoAdminConfig());
        var server = conn.GetServer(conn.GetEndPoints()[0]);

        // none of these CLIENT sub-commands are admin, so they must not trip the admin-mode guard
        // even though AllowAdmin is disabled (regression: the ad-hoc ExecuteMessage previously did
        // not expose its sub-command to Message.IsAdmin, so CLIENT was treated as wholesale-admin)
        var id = server.Execute("CLIENT", "ID");
        ((long)id > 0).Should().BeTrue();

        ((string?)server.Execute("CLIENT", "SETNAME", "roundtrip")).Should().Be("OK");
        ((string?)server.Execute("CLIENT", "GETNAME")).Should().Be("roundtrip");
    }

    [Fact]
    public async Task admin_client_sub_commands_still_require_admin()
    {
        await using var conn = await ConnectionMultiplexer.ConnectAsync(NoAdminConfig());
        var server = conn.GetServer(conn.GetEndPoints()[0]);

        // CLIENT LIST is a genuine admin sub-command (not in the allow-list), so it must still be
        // blocked when AllowAdmin is disabled - the fix must not blanket-allow every CLIENT usage
        var ex = Assert.Throws<RedisCommandException>(() => server.Execute("CLIENT", "LIST"));
        ex.Message.Should().Contain("admin mode");
    }
}
