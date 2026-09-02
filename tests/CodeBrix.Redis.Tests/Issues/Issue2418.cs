using System.Linq;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.Issues; //was previously: StackExchange.Redis.Tests.Issues;

public class Issue2418(ITestOutputHelper output, SharedConnectionFixture? fixture = null) : TestBase(output, fixture)
{
    [Fact]
    public async Task execute()
    {
        await using var conn = Create();
        var db = conn.GetDatabase();

        RedisKey key = Me();
        RedisValue someInt = 12;
        someInt.IsNullOrEmpty.Should().BeFalse(nameof(someInt.IsNullOrEmpty) + " before");
        someInt.IsInteger.Should().BeTrue(nameof(someInt.IsInteger) + " before");
        await db.HashSetAsync(key, [new HashEntry("some_int", someInt)]);

        // check we can fetch it
        var entry = await db.HashGetAllAsync(key);
        entry.Should().NotBeEmpty();
        entry.Should().ContainSingle();
        foreach (var pair in entry)
        {
            Log($"'{pair.Name}'='{pair.Value}'");
        }

        // filter with LINQ
        entry.Any(x => x.Name == "some_int").Should().BeTrue("Any");
        someInt = entry.FirstOrDefault(x => x.Name == "some_int").Value;
        Log($"found via Any: '{someInt}'");
        someInt.IsNullOrEmpty.Should().BeFalse(nameof(someInt.IsNullOrEmpty) + " after");
        someInt.TryParse(out int i).Should().BeTrue();
        i.Should().Be(12);
        ((int)someInt).Should().Be(12);
    }
}
