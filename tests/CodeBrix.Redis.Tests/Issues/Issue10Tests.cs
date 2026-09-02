using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.Issues; //was previously: StackExchange.Redis.Tests.Issues;

public class Issue10Tests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task execute()
    {
        await using var conn = Create();

        var key = Me();
        var db = conn.GetDatabase();
        _ = db.KeyDeleteAsync(key); // contents: nil
        _ = db.ListLeftPushAsync(key, "abc"); // "abc"
        _ = db.ListLeftPushAsync(key, "def"); // "def", "abc"
        _ = db.ListLeftPushAsync(key, "ghi"); // "ghi", "def", "abc",
        _ = db.ListSetByIndexAsync(key, 1, "jkl"); // "ghi", "jkl", "abc"

        var contents = await db.ListRangeAsync(key, 0, -1);
        contents.Length.Should().Be(3);
        contents[0].Should().Be("ghi");
        contents[1].Should().Be("jkl");
        contents[2].Should().Be("abc");
    }
}
