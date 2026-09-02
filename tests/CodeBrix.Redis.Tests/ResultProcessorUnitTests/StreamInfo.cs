using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

public class StreamInfo(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    [Fact]
    public void basic_format_success()
    {
        // XINFO STREAM mystream (basic format, not FULL)
        // Interleaved key-value array with entries like first-entry and last-entry as nested arrays
        var resp = "*32\r\n" +
                   "$6\r\nlength\r\n" +
                   ":2\r\n" +
                   "$15\r\nradix-tree-keys\r\n" +
                   ":1\r\n" +
                   "$16\r\nradix-tree-nodes\r\n" +
                   ":2\r\n" +
                   "$17\r\nlast-generated-id\r\n" +
                   "$15\r\n1638125141232-0\r\n" +
                   "$20\r\nmax-deleted-entry-id\r\n" +
                   "$3\r\n0-0\r\n" +
                   "$13\r\nentries-added\r\n" +
                   ":2\r\n" +
                   "$23\r\nrecorded-first-entry-id\r\n" +
                   "$15\r\n1719505260513-0\r\n" +
                   "$13\r\nidmp-duration\r\n" +
                   ":100\r\n" +
                   "$12\r\nidmp-maxsize\r\n" +
                   ":100\r\n" +
                   "$12\r\npids-tracked\r\n" +
                   ":1\r\n" +
                   "$12\r\niids-tracked\r\n" +
                   ":1\r\n" +
                   "$10\r\niids-added\r\n" +
                   ":1\r\n" +
                   "$15\r\niids-duplicates\r\n" +
                   ":0\r\n" +
                   "$6\r\ngroups\r\n" +
                   ":1\r\n" +
                   "$11\r\nfirst-entry\r\n" +
                   "*2\r\n" +
                   "$15\r\n1638125133432-0\r\n" +
                   "*2\r\n" +
                   "$7\r\nmessage\r\n" +
                   "$5\r\napple\r\n" +
                   "$10\r\nlast-entry\r\n" +
                   "*2\r\n" +
                   "$15\r\n1638125141232-0\r\n" +
                   "*2\r\n" +
                   "$7\r\nmessage\r\n" +
                   "$6\r\nbanana\r\n";

        var result = Execute(resp, ResultProcessor.StreamInfo);

        result.Length.Should().Be(2);
        result.RadixTreeKeys.Should().Be(1);
        result.RadixTreeNodes.Should().Be(2);
        result.ConsumerGroupCount.Should().Be(1);
        result.LastGeneratedId.ToString().Should().Be("1638125141232-0");
        result.MaxDeletedEntryId.ToString().Should().Be("0-0");
        result.EntriesAdded.Should().Be(2);
        result.RecordedFirstEntryId.ToString().Should().Be("1719505260513-0");
        result.IdmpDuration.Should().Be(100);
        result.IdmpMaxSize.Should().Be(100);
        result.PidsTracked.Should().Be(1);
        result.IidsTracked.Should().Be(1);
        result.IidsAdded.Should().Be(1);
        result.IidsDuplicates.Should().Be(0);

        result.FirstEntry.Id.ToString().Should().Be("1638125133432-0");
        result.FirstEntry["message"].Should().Be("apple");

        result.LastEntry.Id.ToString().Should().Be("1638125141232-0");
        result.LastEntry["message"].Should().Be("banana");
    }

    [Fact]
    public void minimal_format_success()
    {
        // Minimal XINFO STREAM response with just required fields
        var resp = "*14\r\n" +
                   "$6\r\nlength\r\n" +
                   ":0\r\n" +
                   "$15\r\nradix-tree-keys\r\n" +
                   ":1\r\n" +
                   "$16\r\nradix-tree-nodes\r\n" +
                   ":1\r\n" +
                   "$6\r\ngroups\r\n" +
                   ":0\r\n" +
                   "$11\r\nfirst-entry\r\n" +
                   "$-1\r\n" +
                   "$10\r\nlast-entry\r\n" +
                   "$-1\r\n" +
                   "$17\r\nlast-generated-id\r\n" +
                   "$3\r\n0-0\r\n";

        var result = Execute(resp, ResultProcessor.StreamInfo);

        result.Length.Should().Be(0);
        result.RadixTreeKeys.Should().Be(1);
        result.RadixTreeNodes.Should().Be(1);
        result.ConsumerGroupCount.Should().Be(0);
        result.FirstEntry.IsNull.Should().BeTrue();
        result.LastEntry.IsNull.Should().BeTrue();
    }

    [Fact]
    public void not_array_failure()
    {
        var resp = "$5\r\nhello\r\n";

        ExecuteUnexpected(resp, ResultProcessor.StreamInfo);
    }

    [Fact]
    public void null_failure()
    {
        var resp = "$-1\r\n";

        ExecuteUnexpected(resp, ResultProcessor.StreamInfo);
    }
}
