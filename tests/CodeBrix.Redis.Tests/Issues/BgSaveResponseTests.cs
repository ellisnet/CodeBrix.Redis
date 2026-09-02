using System.Threading.Tasks;
using Xunit;

namespace CodeBrix.Redis.Tests.Issues; //was previously: StackExchange.Redis.Tests.Issues;

public class BgSaveResponseTests(ITestOutputHelper output) : TestBase(output)
{
    [Theory]
    [InlineData(SaveType.BackgroundSave)]
    [InlineData(SaveType.BackgroundRewriteAppendOnlyFile)]
    public async Task shouldnt_throw_exception(SaveType saveType)
    {
        Assert.Skip("We don't need to test this, and it really screws local testing hard.");

        await using var conn = Create(allowAdmin: true);

        var server = GetServer(conn);
        server.Save(saveType);
        await Task.Delay(1000, TestContext.Current.CancellationToken);
    }
}
