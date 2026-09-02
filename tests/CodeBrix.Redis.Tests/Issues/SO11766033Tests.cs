using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.Issues; //was previously: StackExchange.Redis.Tests.Issues;

public class SO11766033Tests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task test_null_string()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        const string? expectedTestValue = null;
        var uid = Me();
        _ = db.StringSetAsync(uid, "abc");
        _ = db.StringSetAsync(uid, expectedTestValue);
        string? testValue = db.StringGet(uid);
        testValue.Should().BeNull();
    }

    [Fact]
    public async Task test_empty_string()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        const string expectedTestValue = "";
        var uid = Me();

        _ = db.StringSetAsync(uid, expectedTestValue);
        string? testValue = db.StringGet(uid);

        testValue.Should().Be(expectedTestValue);
    }
}
