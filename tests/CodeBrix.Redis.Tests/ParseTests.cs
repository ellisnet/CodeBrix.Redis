using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Redis.Configuration;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class ParseTests(ITestOutputHelper output) : TestBase(output)
{
    public static IEnumerable<object[]> GetTestData()
    {
        yield return new object[] { "$4\r\nPING\r\n$4\r\nPON", 1 };
        yield return new object[] { "$4\r\nPING\r\n$4\r\nPONG", 1 };
        yield return new object[] { "$4\r\nPING\r\n$4\r\nPONG\r", 1 };
        yield return new object[] { "$4\r\nPING\r\n$4\r\nPONG\r\n", 2 };
        yield return new object[] { "$4\r\nPING\r\n$4\r\nPONG\r\n$4", 2 };
        yield return new object[] { "$4\r\nPING\r\n$4\r\nPONG\r\n$4\r", 2 };
        yield return new object[] { "$4\r\nPING\r\n$4\r\nPONG\r\n$4\r\n", 2 };
        yield return new object[] { "$4\r\nPING\r\n$4\r\nPONG\r\n$4\r\nP", 2 };
        yield return new object[] { "$4\r\nPING\r\n$4\r\nPONG\r\n$4\r\nPO", 2 };
        yield return new object[] { "$4\r\nPING\r\n$4\r\nPONG\r\n$4\r\nPON", 2 };
        yield return new object[] { "$4\r\nPING\r\n$4\r\nPONG\r\n$4\r\nPONG", 2 };
        yield return new object[] { "$4\r\nPING\r\n$4\r\nPONG\r\n$4\r\nPONG\r", 2 };
        yield return new object[] { "$4\r\nPING\r\n$4\r\nPONG\r\n$4\r\nPONG\r\n", 3 };
        yield return new object[] { "$4\r\nPING\r\n$4\r\nPONG\r\n$4\r\nPONG\r\n$", 3 };
    }

    [Theory(Timeout = 1000)]
    [MemberData(nameof(GetTestData))]
    [Obsolete("Calls the [Obsolete] ProcessMessagesAsync helper; see its note")]
    public Task parse_as_single_chunk(string ascii, int expected)
    {
        var buffer = new ReadOnlySequence<byte>(Encoding.ASCII.GetBytes(ascii));
        return ProcessMessagesAsync(buffer, expected, TestContext.Current.CancellationToken);
    }

    [Theory(Timeout = 1000)]
    [MemberData(nameof(GetTestData))]
    [Obsolete("Calls the [Obsolete] ProcessMessagesAsync helper; see its note")]
    public Task parse_as_lots_of_chunks(string ascii, int expected)
    {
        var bytes = Encoding.ASCII.GetBytes(ascii);
        var chunks = new ReadOnlyMemory<byte>[bytes.Length];
        for (int i = 0; i < bytes.Length; i++)
        {
            chunks[i] = new ReadOnlyMemory<byte>(bytes, i, 1);
        }
        var buffer = FragmentedSegment<byte>.Create(chunks);
        buffer.Length.Should().Be(bytes.Length);
        return ProcessMessagesAsync(buffer, expected, TestContext.Current.CancellationToken);
    }

    //[Obsolete] on the helper, not a suppression: LoggingTunnel is marked [Obsolete] as an
    //"experimental" gate and its StreamRespReader is what this test parses with. C# does not
    //report CS0618 inside a member that is itself obsolete.
    [Obsolete("Exercises the [Obsolete] LoggingTunnel diagnostics API, deliberately")]
    private async Task ProcessMessagesAsync(ReadOnlySequence<byte> buffer, int expected, CancellationToken cancel, bool isInbound = false)
    {
        Log($"chain: {buffer.Length}");
        MemoryStream ms;
        if (buffer.IsSingleSegment && MemoryMarshal.TryGetArray(buffer.First, out var segment))
        {
            // use existing buffer
            ms = new MemoryStream(segment.Array!, segment.Offset, (int)buffer.Length, false, true);
        }
        else
        {
            ms = new MemoryStream(checked((int)buffer.Length));
            foreach (var chunk in buffer)
            {
                ms.Write(chunk.Span);
            }

            ms.Position = 0;
        }

        var reader = new LoggingTunnel.StreamRespReader(ms, isInbound: isInbound);
        int found = 0;
        while (!cancel.IsCancellationRequested)
        {
            var oldPos = reader.Position;
            var result = await reader.ReadOneAsync(cancel).ForAwait();
            if (result.Result is null) break;
            Log($"[{oldPos},{reader.Position}): {result} - {result.Result}");
            found++;
        }
        cancel.ThrowIfCancellationRequested();
        found.Should().Be(expected);
    }
}
