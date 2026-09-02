using System;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

public class Scan(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    // SCAN/SSCAN format: array of 2 elements [cursor, array of keys]
    // Example: *2\r\n$1\r\n0\r\n*3\r\n$3\r\nkey1\r\n$3\r\nkey2\r\n$3\r\nkey3\r\n
    [Theory]
    [InlineData("*2\r\n$1\r\n0\r\n*0\r\n", 0L, 0)] // cursor 0, empty array
    [InlineData("*2\r\n$1\r\n5\r\n*0\r\n", 5L, 0)] // cursor 5, empty array
    [InlineData("*2\r\n$1\r\n0\r\n*1\r\n$3\r\nfoo\r\n", 0L, 1)] // cursor 0, 1 key
    [InlineData("*2\r\n$1\r\n0\r\n*3\r\n$4\r\nkey1\r\n$4\r\nkey2\r\n$4\r\nkey3\r\n", 0L, 3)] // cursor 0, 3 keys
    [InlineData("*2\r\n$2\r\n42\r\n*2\r\n$4\r\ntest\r\n$5\r\nhello\r\n", 42L, 2)] // cursor 42, 2 keys
    public void set_scan_result_processor_valid_input(string resp, long expectedCursor, int expectedCount)
    {
        //Arrange
        var processor = RedisDatabase.SetScanResultProcessor.Default;

        //Act
        var result = Execute(resp, processor);

        //Assert
        result.Cursor.Should().Be(expectedCursor);
        result.Count.Should().Be(expectedCount);
    }

    [Fact]
    public void set_scan_result_processor_validates_content()
    {
        // cursor 0, 3 keys: "key1", "key2", "key3"
        var resp = "*2\r\n$1\r\n0\r\n*3\r\n$4\r\nkey1\r\n$4\r\nkey2\r\n$4\r\nkey3\r\n";
        var processor = RedisDatabase.SetScanResultProcessor.Default;
        var result = Execute(resp, processor);

        result.Cursor.Should().Be(0L);
        result.Count.Should().Be(3);

        // Access the values through the result
        var values = result.Values;
        values.Length.Should().Be(3);
        ((string?)values[0]).Should().Be("key1");
        ((string?)values[1]).Should().Be("key2");
        ((string?)values[2]).Should().Be("key3");

        result.Recycle();
    }

    // HSCAN format: array of 2 elements [cursor, interleaved array of field/value pairs]
    // Example: *2\r\n$1\r\n0\r\n*4\r\n$6\r\nfield1\r\n$6\r\nvalue1\r\n$6\r\nfield2\r\n$6\r\nvalue2\r\n
    [Theory]
    [InlineData("*2\r\n$1\r\n0\r\n*0\r\n", 0L, 0)] // cursor 0, empty array
    [InlineData("*2\r\n$1\r\n7\r\n*0\r\n", 7L, 0)] // cursor 7, empty array
    [InlineData("*2\r\n$1\r\n0\r\n*2\r\n$3\r\nfoo\r\n$3\r\nbar\r\n", 0L, 1)] // cursor 0, 1 pair
    [InlineData("*2\r\n$1\r\n0\r\n*4\r\n$2\r\nf1\r\n$2\r\nv1\r\n$2\r\nf2\r\n$2\r\nv2\r\n", 0L, 2)] // cursor 0, 2 pairs
    [InlineData("*2\r\n$2\r\n99\r\n*6\r\n$1\r\na\r\n$1\r\n1\r\n$1\r\nb\r\n$1\r\n2\r\n$1\r\nc\r\n$1\r\n3\r\n", 99L, 3)] // cursor 99, 3 pairs
    public void hash_scan_result_processor_valid_input(string resp, long expectedCursor, int expectedCount)
    {
        //Arrange
        var processor = RedisDatabase.HashScanResultProcessor.Default;

        //Act
        var result = Execute(resp, processor);

        //Assert
        result.Cursor.Should().Be(expectedCursor);
        result.Count.Should().Be(expectedCount);
    }

    [Fact]
    public void hash_scan_result_processor_validates_content()
    {
        // cursor 0, 2 pairs: "field1"="value1", "field2"="value2"
        var resp = "*2\r\n$1\r\n0\r\n*4\r\n$6\r\nfield1\r\n$6\r\nvalue1\r\n$6\r\nfield2\r\n$6\r\nvalue2\r\n";
        var processor = RedisDatabase.HashScanResultProcessor.Default;
        var result = Execute(resp, processor);

        result.Cursor.Should().Be(0L);
        result.Count.Should().Be(2);

        var entries = result.Values;
        entries.Length.Should().Be(2);
        ((string?)entries[0].Name).Should().Be("field1");
        ((string?)entries[0].Value).Should().Be("value1");
        ((string?)entries[1].Name).Should().Be("field2");
        ((string?)entries[1].Value).Should().Be("value2");

        result.Recycle();
    }

    // ZSCAN format: array of 2 elements [cursor, interleaved array of member/score pairs]
    // Example: *2\r\n$1\r\n0\r\n*4\r\n$7\r\nmember1\r\n$3\r\n1.5\r\n$7\r\nmember2\r\n$3\r\n2.5\r\n
    [Theory]
    [InlineData("*2\r\n$1\r\n0\r\n*0\r\n", 0L, 0)] // cursor 0, empty array
    [InlineData("*2\r\n$2\r\n10\r\n*0\r\n", 10L, 0)] // cursor 10, empty array
    [InlineData("*2\r\n$1\r\n0\r\n*2\r\n$3\r\nfoo\r\n$1\r\n1\r\n", 0L, 1)] // cursor 0, 1 pair
    [InlineData("*2\r\n$1\r\n0\r\n*4\r\n$2\r\nm1\r\n$3\r\n1.5\r\n$2\r\nm2\r\n$3\r\n2.5\r\n", 0L, 2)] // cursor 0, 2 pairs
    [InlineData("*2\r\n$2\r\n88\r\n*6\r\n$1\r\na\r\n$1\r\n1\r\n$1\r\nb\r\n$1\r\n2\r\n$1\r\nc\r\n$1\r\n3\r\n", 88L, 3)] // cursor 88, 3 pairs
    public void sorted_set_scan_result_processor_valid_input(string resp, long expectedCursor, int expectedCount)
    {
        //Arrange
        var processor = RedisDatabase.SortedSetScanResultProcessor.Default;

        //Act
        var result = Execute(resp, processor);

        //Assert
        result.Cursor.Should().Be(expectedCursor);
        result.Count.Should().Be(expectedCount);
    }

    [Fact]
    public void sorted_set_scan_result_processor_validates_content()
    {
        // cursor 0, 2 pairs: "member1"=1.5, "member2"=2.5
        var resp = "*2\r\n$1\r\n0\r\n*4\r\n$7\r\nmember1\r\n$3\r\n1.5\r\n$7\r\nmember2\r\n$3\r\n2.5\r\n";
        var processor = RedisDatabase.SortedSetScanResultProcessor.Default;
        var result = Execute(resp, processor);

        result.Cursor.Should().Be(0L);
        result.Count.Should().Be(2);

        var entries = result.Values;
        entries.Length.Should().Be(2);
        ((string?)entries[0].Element).Should().Be("member1");
        entries[0].Score.Should().Be(1.5);
        ((string?)entries[1].Element).Should().Be("member2");
        entries[1].Score.Should().Be(2.5);

        result.Recycle();
    }

    [Theory]
    [InlineData("*1\r\n$1\r\n0\r\n")] // only 1 element instead of 2
    [InlineData("*3\r\n$1\r\n0\r\n*0\r\n$4\r\nextra\r\n")] // 3 elements instead of 2
    [InlineData("$1\r\n0\r\n")] // scalar instead of array
    public void scan_processors_invalid_format(string resp)
    {
        ExecuteUnexpected(resp, RedisDatabase.SetScanResultProcessor.Default, caller: nameof(RedisDatabase.SetScanResultProcessor));
        ExecuteUnexpected(resp, RedisDatabase.HashScanResultProcessor.Default, caller: nameof(RedisDatabase.HashScanResultProcessor));
        ExecuteUnexpected(resp, RedisDatabase.SortedSetScanResultProcessor.Default, caller: nameof(RedisDatabase.SortedSetScanResultProcessor));
    }
}
