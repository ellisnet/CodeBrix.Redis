using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class ProductVariantUnitTests(ITestOutputHelper log)
{
    [Theory]
    [InlineData(ProductVariant.Redis)]
    [InlineData(ProductVariant.Valkey)]
    [InlineData(ProductVariant.Garnet)]
    public async Task detect_product_variant(ProductVariant variant)
    {
        using var serverObj = new ProductServer(variant, log);
        using var conn = await serverObj.ConnectAsync(withPubSub: false);
        var serverApi = conn.GetServer(conn.GetEndPoints().First());
        serverApi.Ping();
        var reportedProduct = serverApi.GetProductVariant(out var reportedVersion);
        reportedProduct.Should().Be(variant);
        log.WriteLine($"Detected {reportedProduct} version: {reportedVersion}");
        if (variant == ProductVariant.Redis)
        {
            reportedVersion.Should().Be(serverObj.VersionString);
        }
        else
        {
            reportedVersion.Should().Be("1.2.3-preview4");
        }
    }

    [Theory]
    [InlineData(ProductVariant.Redis, ServerType.Standalone, true)]
    [InlineData(ProductVariant.Redis, ServerType.Cluster, false)]
    [InlineData(ProductVariant.Garnet, ServerType.Standalone, true)]
    [InlineData(ProductVariant.Garnet, ServerType.Cluster, false)]
    [InlineData(ProductVariant.Valkey, ServerType.Standalone, true)]
    [InlineData(ProductVariant.Valkey, ServerType.Cluster, true)]
    public async Task multi_db_support_matches_product_variant_and_server_type(ProductVariant variant, ServerType serverType, bool supportsMultiDb)
    {
        using var serverObj = new ProductServer(variant, log, serverType);
        await using var conn = await serverObj.ConnectAsync(withPubSub: false);

        var serverApi = conn.GetServer(conn.GetEndPoints().First());
        await serverApi.PingAsync();
        serverApi.ServerType.Should().Be(serverType);
        serverApi.GetProductVariant(out _).Should().Be(variant);

        RedisKey key = $"multidb:{variant}:{serverType}";
        const string db0Value = "db0";
        const string db1Value = "db1";
        var db0 = conn.GetDatabase(0);

        var db1 = conn.GetDatabase(1);

        await db0.StringSetAsync(key, db0Value);

        if (supportsMultiDb)
        {
            await db1.StringSetAsync(key, db1Value);
            ((string?)await db0.StringGetAsync(key)).Should().Be(db0Value);
            ((string?)await db1.StringGetAsync(key)).Should().Be(db1Value);
        }
        else
        {
            var ex = await Assert.ThrowsAsync<RedisConnectionException>(() => db1.StringSetAsync(key, db1Value));
            var inner = Assert.IsType<RedisCommandException>(ex.InnerException);
            inner.Message.Should().Contain("cannot switch to database: 1");
            ((string?)await db0.StringGetAsync(key)).Should().Be(db0Value);
        }
    }

    private sealed class ProductServer : InProcessTestServer
    {
        private readonly ProductVariant _variant;

        public ProductServer(ProductVariant variant, ITestOutputHelper log, ServerType serverType = ServerType.Standalone)
            : base(log)
        {
            _variant = variant;
            ServerType = serverType;
        }

        protected override void Info(StringBuilder sb, string section)
        {
            base.Info(sb, section);
            if (section is "Server")
            {
                switch (_variant)
                {
                    case ProductVariant.Garnet:
                        sb.AppendLine("garnet_version:1.2.3-preview4");
                        break;
                    case ProductVariant.Valkey:
                        sb.AppendLine("valkey_version:1.2.3-preview4")
                            .AppendLine("server_name:valkey");
                        break;
                }
            }
        }

        protected override bool SupportMultiDb(out string err)
        {
            switch (_variant)
            {
                case ProductVariant.Valkey:
                    // support multiple databases even on cluster
                    err = "";
                    return true;
                default:
                    return base.SupportMultiDb(out err);
            }
        }
    }
}
