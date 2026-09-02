using System.Text;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class ValkeyUnitTests(ITestOutputHelper log)
{
    [Theory]
    [InlineData(ServerType.Standalone)]
    [InlineData(ServerType.Cluster)]
    public async Task identify_valkey_cluster(ServerType type)
    {
        using InProcessValkeyLikeServer server = new(log) { ServerType = type };
        await using var client = await server.ConnectAsync();
        var serverApi = client.GetServer(server.DefaultEndPoint);
        serverApi.ServerType.Should().Be(type);
        serverApi.GetProductVariant(out var version).Should().Be(ProductVariant.Valkey);
        version.Should().Be("8.1");
    }

    private sealed class InProcessValkeyLikeServer(ITestOutputHelper log) : InProcessTestServer(log)
    {
        // see https://github.com/StackExchange/StackExchange.Redis/pull/3050
        protected override string ServerModeKey => "server_mode";

        protected override void Info(StringBuilder sb, string section)
        {
            base.Info(sb, section);
            if (section is "Server")
            {
                sb.AppendLine("valkey_version:8.1")
                    .AppendLine("server_name:valkey");
            }
        }
    }
}
