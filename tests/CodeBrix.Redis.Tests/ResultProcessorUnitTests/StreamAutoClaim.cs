using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

public class StreamAutoClaim(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    [Fact]
    public void with_entries_three_elements_success()
    {
        // XAUTOCLAIM mystream mygroup Alice 3600000 0-0 COUNT 25
        // 1) "0-0"
        // 2) 1) 1) "1609338752495-0"
        //       2) 1) "field"
        //          2) "value"
        // 3) (empty array)
        var resp = "*3\r\n" +
                   "$3\r\n0-0\r\n" +
                   "*1\r\n" + // Array of 1 entry
                   "*2\r\n" + // Entry: [id, fields]
                   "$15\r\n1609338752495-0\r\n" +
                   "*2\r\n" + // Fields array
                   "$5\r\nfield\r\n" +
                   "$5\r\nvalue\r\n" +
                   "*0\r\n";  // Empty deleted IDs array

        var result = Execute(resp, ResultProcessor.StreamAutoClaim);

        result.NextStartId.ToString().Should().Be("0-0");
        result.ClaimedEntries.Should().ContainSingle();
        result.ClaimedEntries[0].Id.ToString().Should().Be("1609338752495-0");
        result.ClaimedEntries[0]["field"].Should().Be("value");
        result.DeletedIds.Should().BeEmpty();
    }

    [Fact]
    public void with_entries_two_elements_older_server_success()
    {
        // Older Redis 6.2 - only returns 2 elements (no deleted IDs)
        var resp = "*2\r\n" +
                   "$3\r\n0-0\r\n" +
                   "*1\r\n" +
                   "*2\r\n" +
                   "$15\r\n1609338752495-0\r\n" +
                   "*2\r\n" +
                   "$5\r\nfield\r\n" +
                   "$5\r\nvalue\r\n";

        var result = Execute(resp, ResultProcessor.StreamAutoClaim);

        result.NextStartId.ToString().Should().Be("0-0");
        result.ClaimedEntries.Should().ContainSingle();
        result.DeletedIds.Should().BeEmpty();
    }

    [Fact]
    public void empty_entries_success()
    {
        // No entries claimed
        var resp = "*3\r\n" +
                   "$3\r\n0-0\r\n" +
                   "*0\r\n" + // Empty entries array
                   "*0\r\n";  // Empty deleted IDs array

        var result = Execute(resp, ResultProcessor.StreamAutoClaim);

        result.NextStartId.ToString().Should().Be("0-0");
        result.ClaimedEntries.Should().BeEmpty();
        result.DeletedIds.Should().BeEmpty();
    }

    [Fact]
    public void null_entries_success()
    {
        // Null entries array (alternative representation)
        var resp = "*3\r\n" +
                   "$3\r\n0-0\r\n" +
                   "$-1\r\n" + // Null entries
                   "*0\r\n";

        var result = Execute(resp, ResultProcessor.StreamAutoClaim);

        result.NextStartId.ToString().Should().Be("0-0");
        result.ClaimedEntries.Should().BeEmpty();
        result.DeletedIds.Should().BeEmpty();
    }

    [Fact]
    public void with_deleted_ids_success()
    {
        // Some entries were deleted
        var resp = "*3\r\n" +
                   "$3\r\n0-0\r\n" +
                   "*0\r\n" + // No claimed entries
                   "*2\r\n" + // 2 deleted IDs
                   "$15\r\n1609338752495-0\r\n" +
                   "$15\r\n1609338752496-0\r\n";

        var result = Execute(resp, ResultProcessor.StreamAutoClaim);

        result.NextStartId.ToString().Should().Be("0-0");
        result.ClaimedEntries.Should().BeEmpty();
        result.DeletedIds.Length.Should().Be(2);
        result.DeletedIds[0].ToString().Should().Be("1609338752495-0");
        result.DeletedIds[1].ToString().Should().Be("1609338752496-0");
    }

    [Fact]
    public void not_array_failure()
    {
        var resp = "$5\r\nhello\r\n";

        ExecuteUnexpected(resp, ResultProcessor.StreamAutoClaim);
    }

    [Fact]
    public void null_failure()
    {
        var resp = "$-1\r\n";

        ExecuteUnexpected(resp, ResultProcessor.StreamAutoClaim);
    }
}
