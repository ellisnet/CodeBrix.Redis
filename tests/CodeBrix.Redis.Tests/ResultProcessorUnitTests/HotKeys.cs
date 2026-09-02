using System;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

public class HotKeys(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    [Fact]
    public void full_format_success()
    {
        // HOTKEYS GET - full response with all fields
        // Carefully counted byte lengths for each string
        var resp = "*1\r\n" +
                   "*24\r\n" +
                   "$15\r\ntracking-active\r\n" +
                   ":0\r\n" +
                   "$12\r\nsample-ratio\r\n" +
                   ":1\r\n" +
                   "$14\r\nselected-slots\r\n" +
                   "*1\r\n" +
                   "*2\r\n" +
                   ":0\r\n" +
                   ":16383\r\n" +
                   "$25\r\nall-commands-all-slots-us\r\n" +
                   ":103\r\n" +
                   "$32\r\nnet-bytes-all-commands-all-slots\r\n" +
                   ":2042\r\n" +
                   "$29\r\ncollection-start-time-unix-ms\r\n" +
                   ":1770824933147\r\n" +
                   "$22\r\ncollection-duration-ms\r\n" +
                   ":0\r\n" +
                   "$22\r\ntotal-cpu-time-user-ms\r\n" +
                   ":23\r\n" +
                   "$21\r\ntotal-cpu-time-sys-ms\r\n" +
                   ":7\r\n" +
                   "$15\r\ntotal-net-bytes\r\n" +
                   ":2038\r\n" +
                   "$14\r\nby-cpu-time-us\r\n" +
                   "*10\r\n" +
                   "$18\r\nhotkey_001_counter\r\n" +
                   ":29\r\n" +
                   "$10\r\nhotkey_001\r\n" +
                   ":25\r\n" +
                   "$15\r\nhotkey_001_hash\r\n" +
                   ":11\r\n" +
                   "$15\r\nhotkey_001_list\r\n" +
                   ":9\r\n" +
                   "$14\r\nhotkey_001_set\r\n" +
                   ":9\r\n" +
                   "$12\r\nby-net-bytes\r\n" +
                   "*10\r\n" +
                   "$10\r\nhotkey_001\r\n" +
                   ":446\r\n" +
                   "$10\r\nhotkey_002\r\n" +
                   ":328\r\n" +
                   "$15\r\nhotkey_001_hash\r\n" +
                   ":198\r\n" +
                   "$14\r\nhotkey_001_set\r\n" +
                   ":167\r\n" +
                   "$18\r\nhotkey_001_counter\r\n" +
                   ":116\r\n";

        var result = Execute(resp, HotKeysResult.Processor);

        Assert.NotNull(result);
        result.TrackingActive.Should().BeFalse();
        result.SampleRatio.Should().Be(1);
        result.AllCommandsAllSlotsMicroseconds.Should().Be(103);
        result.AllCommandsAllSlotsNetworkBytes.Should().Be(2042);
        result.CollectionStartTimeUnixMilliseconds.Should().Be(1770824933147);
        result.CollectionDurationMicroseconds.Should().Be(0);
        result.TotalCpuTimeUserMicroseconds.Should().Be(23000);
        result.TotalCpuTimeSystemMicroseconds.Should().Be(7000);
        result.TotalNetworkBytes.Should().Be(2038);

        // Validate TimeSpan properties
        // 103 microseconds = 0.103 milliseconds
        result.AllCommandsAllSlotsTime.TotalMilliseconds.Should().BeApproximately(0.103, 1e-10);
        result.CollectionDuration.Should().Be(TimeSpan.Zero);
        // 23000 microseconds = 23 milliseconds
        result.TotalCpuTimeUser!.Value.TotalMilliseconds.Should().BeApproximately(23.0, 1e-10);
        // 7000 microseconds = 7 milliseconds
        result.TotalCpuTimeSystem!.Value.TotalMilliseconds.Should().BeApproximately(7.0, 1e-10);
        // 30000 microseconds = 30 milliseconds
        result.TotalCpuTime!.Value.TotalMilliseconds.Should().BeApproximately(30.0, 1e-10);

        // Validate by-cpu-time-us array
        result.CpuByKey.Length.Should().Be(5);
        ((string?)result.CpuByKey[0].Key).Should().Be("hotkey_001_counter");
        result.CpuByKey[0].DurationMicroseconds.Should().Be(29);
        ((string?)result.CpuByKey[1].Key).Should().Be("hotkey_001");
        result.CpuByKey[1].DurationMicroseconds.Should().Be(25);
        ((string?)result.CpuByKey[2].Key).Should().Be("hotkey_001_hash");
        result.CpuByKey[2].DurationMicroseconds.Should().Be(11);
        ((string?)result.CpuByKey[3].Key).Should().Be("hotkey_001_list");
        result.CpuByKey[3].DurationMicroseconds.Should().Be(9);
        ((string?)result.CpuByKey[4].Key).Should().Be("hotkey_001_set");
        result.CpuByKey[4].DurationMicroseconds.Should().Be(9);

        // Validate by-net-bytes array
        result.NetworkBytesByKey.Length.Should().Be(5);
        ((string?)result.NetworkBytesByKey[0].Key).Should().Be("hotkey_001");
        result.NetworkBytesByKey[0].Bytes.Should().Be(446);
        ((string?)result.NetworkBytesByKey[1].Key).Should().Be("hotkey_002");
        result.NetworkBytesByKey[1].Bytes.Should().Be(328);
        ((string?)result.NetworkBytesByKey[2].Key).Should().Be("hotkey_001_hash");
        result.NetworkBytesByKey[2].Bytes.Should().Be(198);
        ((string?)result.NetworkBytesByKey[3].Key).Should().Be("hotkey_001_set");
        result.NetworkBytesByKey[3].Bytes.Should().Be(167);
        ((string?)result.NetworkBytesByKey[4].Key).Should().Be("hotkey_001_counter");
        result.NetworkBytesByKey[4].Bytes.Should().Be(116);
    }

    [Fact]
    public void minimal_format_success()
    {
        // Minimal HOTKEYS response with just tracking-active
        var resp = "*1\r\n" +
                   "*2\r\n" +
                   "$15\r\ntracking-active\r\n" +
                   ":1\r\n";

        var result = Execute(resp, HotKeysResult.Processor);

        Assert.NotNull(result);
        result.TrackingActive.Should().BeTrue();
    }

    [Fact]
    public void not_array_failure()
    {
        var resp = "$5\r\nhello\r\n";

        ExecuteUnexpected(resp, HotKeysResult.Processor);
    }

    [Fact]
    public void null_success()
    {
        //Arrange
        var resp = "$-1\r\n";

        //Act
        var result = Execute(resp, HotKeysResult.Processor);

        //Assert
        result.Should().BeNull();
    }
}
