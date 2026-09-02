using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class ConstraintsTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    [Fact]
    public void value_equals()
    {
        RedisValue x = 1, y = "1";
        x.Equals(y).Should().BeTrue("equals");
        (x == y).Should().BeTrue("operator");
    }

    [Fact]
    public async Task test_manual_incr()
    {
        await using var conn = Create(syncTimeout: 120000); // big timeout while debugging

        var key = Me();
        var db = conn.GetDatabase();
        for (int i = 0; i < 10; i++)
        {
            db.KeyDelete(key, CommandFlags.FireAndForget);
            (await ManualIncrAsync(db, key).ForAwait()).Should().Be(1);
            (await ManualIncrAsync(db, key).ForAwait()).Should().Be(2);
            ((long)db.StringGet(key)).Should().Be(2);
        }
    }

    public static async Task<long?> ManualIncrAsync(IDatabase connection, RedisKey key)
    {
        var oldVal = (long?)await connection.StringGetAsync(key).ForAwait();
        var newVal = (oldVal ?? 0) + 1;
        var tran = connection.CreateTransaction();
        { // check hasn't changed
            // Deliberately the long way round: this exercises the optimistic-concurrency path (read, compare,
            // conditional write, observe the abort), which is the thing under test. StringIncrement would be
            // the right answer in real code, and a single compare-and-set write would remove the abort we
            // are here to provoke.
            tran.AddCondition(Condition.StringEqual(key, oldVal));
            _ = tran.StringSetAsync(key, newVal);
            if (!await tran.ExecuteAsync().ForAwait()) return null; // aborted
            return newVal;
        }
    }
}
