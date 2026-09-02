using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

public class StreamAutoClaimIdsOnly(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    [Fact]
    public void with_ids_three_elements_success()
    {
        // XAUTOCLAIM mystream mygroup Alice 3600000 0-0 COUNT 25 JUSTID
        // 1) "0-0"
        // 2) 1) "1609338752495-0"
        //    2) "1609338752496-0"
        // 3) (empty array)
        var resp = "*3\r\n" +
                   "$3\r\n0-0\r\n" +
                   "*2\r\n" + // Array of 2 claimed IDs
                   "$15\r\n1609338752495-0\r\n" +
                   "$15\r\n1609338752496-0\r\n" +
                   "*0\r\n";  // Empty deleted IDs array

        var result = Execute(resp, ResultProcessor.StreamAutoClaimIdsOnly);

        result.NextStartId.ToString().Should().Be("0-0");
        result.ClaimedIds.Length.Should().Be(2);
        result.ClaimedIds[0].ToString().Should().Be("1609338752495-0");
        result.ClaimedIds[1].ToString().Should().Be("1609338752496-0");
        result.DeletedIds.Should().BeEmpty();
    }

    [Fact]
    public void with_ids_two_elements_older_server_success()
    {
        // Older Redis 6.2 - only returns 2 elements (no deleted IDs)
        var resp = "*2\r\n" +
                   "$3\r\n0-0\r\n" +
                   "*2\r\n" +
                   "$15\r\n1609338752495-0\r\n" +
                   "$15\r\n1609338752496-0\r\n";

        var result = Execute(resp, ResultProcessor.StreamAutoClaimIdsOnly);

        result.NextStartId.ToString().Should().Be("0-0");
        result.ClaimedIds.Length.Should().Be(2);
        result.DeletedIds.Should().BeEmpty();
    }

    [Fact]
    public void empty_ids_success()
    {
        // No IDs claimed
        var resp = "*3\r\n" +
                   "$3\r\n0-0\r\n" +
                   "*0\r\n" + // Empty claimed IDs array
                   "*0\r\n";  // Empty deleted IDs array

        var result = Execute(resp, ResultProcessor.StreamAutoClaimIdsOnly);

        result.NextStartId.ToString().Should().Be("0-0");
        result.ClaimedIds.Should().BeEmpty();
        result.DeletedIds.Should().BeEmpty();
    }

    [Fact]
    public void null_ids_success()
    {
        // Null IDs array (alternative representation)
        var resp = "*3\r\n" +
                   "$3\r\n0-0\r\n" +
                   "$-1\r\n" + // Null claimed IDs
                   "*0\r\n";

        var result = Execute(resp, ResultProcessor.StreamAutoClaimIdsOnly);

        result.NextStartId.ToString().Should().Be("0-0");
        result.ClaimedIds.Should().BeEmpty();
        result.DeletedIds.Should().BeEmpty();
    }

    [Fact]
    public void with_deleted_ids_success()
    {
        // Some entries were deleted
        var resp = "*3\r\n" +
                   "$3\r\n0-0\r\n" +
                   "*1\r\n" + // 1 claimed ID
                   "$15\r\n1609338752495-0\r\n" +
                   "*2\r\n" + // 2 deleted IDs
                   "$15\r\n1609338752496-0\r\n" +
                   "$15\r\n1609338752497-0\r\n";

        var result = Execute(resp, ResultProcessor.StreamAutoClaimIdsOnly);

        result.NextStartId.ToString().Should().Be("0-0");
        result.ClaimedIds.Should().ContainSingle();
        result.ClaimedIds[0].ToString().Should().Be("1609338752495-0");
        result.DeletedIds.Length.Should().Be(2);
        result.DeletedIds[0].ToString().Should().Be("1609338752496-0");
        result.DeletedIds[1].ToString().Should().Be("1609338752497-0");
    }

    [Fact]
    public void not_array_failure()
    {
        var resp = "$5\r\nhello\r\n";

        ExecuteUnexpected(resp, ResultProcessor.StreamAutoClaimIdsOnly);
    }

    [Fact]
    public void null_failure()
    {
        var resp = "$-1\r\n";

        ExecuteUnexpected(resp, ResultProcessor.StreamAutoClaimIdsOnly);
    }
}
