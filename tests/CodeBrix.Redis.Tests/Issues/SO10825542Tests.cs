using System;
using System.Text;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.Issues; //was previously: StackExchange.Redis.Tests.Issues;

public class SO10825542Tests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task execute()
    {
        await using var conn = Create();
        var key = Me();

        var db = conn.GetDatabase();
        // set the field value and expiration
        _ = db.HashSetAsync(key, "field1", Encoding.UTF8.GetBytes("hello world"));
        _ = db.KeyExpireAsync(key, TimeSpan.FromSeconds(7200));
        _ = db.HashSetAsync(key, "field2", "fooobar");
        var result = await db.HashGetAllAsync(key).ForAwait();

        result.Length.Should().Be(2);
        var dict = result.ToStringDictionary();
        dict["field1"].Should().Be("hello world");
        dict["field2"].Should().Be("fooobar");
    }
}
