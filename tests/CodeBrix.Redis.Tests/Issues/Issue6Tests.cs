using System.Threading.Tasks;
using Xunit;

namespace CodeBrix.Redis.Tests.Issues; //was previously: StackExchange.Redis.Tests.Issues;

public class Issue6Tests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task should_work_without_echo_or_ping()
    {
        await using var conn = Create(proxy: Proxy.Twemproxy);

        Log("config: " + conn.Configuration);
        var db = conn.GetDatabase();
        var time = await db.PingAsync();
        Log("ping time: " + time);
    }
}
