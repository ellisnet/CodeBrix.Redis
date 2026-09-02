using System;
using System.Buffers;
using System.Text;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

/// <summary>
/// Round-trips a multi-segment <see cref="ReadOnlySequence{T}"/>-backed <see cref="RedisValue"/>
/// (<see cref="RedisValue.StorageType.Sequence"/>) through the shared in-process server via
/// StringSet/StringGet, exercising the segmented write path in <c>MessageWriter</c>.
/// </summary>
public class RedisValueSequenceServerTests(ITestOutputHelper output, InProcServerFixture fixture) : TestBase(output, fixture)
{
    // one segment per byte => a genuinely multi-segment sequence (StorageType.Sequence)
    private static RedisValue MultiSegment(byte[] payload)
    {
        var chunks = new ReadOnlyMemory<byte>[payload.Length];
        for (int i = 0; i < payload.Length; i++)
        {
            chunks[i] = new ReadOnlyMemory<byte>(payload, i, 1);
        }
        return FragmentedSegment<byte>.Create(chunks);
    }

    [Fact]
    public async Task string_set_multi_segment_sequence_round_trips()
    {
        await using var conn = Create();
        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        var payload = Encoding.UTF8.GetBytes("the quick brown fox jumps over the lazy dog");
        RedisValue value = MultiSegment(payload);
        value.Type.Should().Be(RedisValue.StorageType.Sequence);

        db.StringSet(key, value).Should().BeTrue();

        var roundTripped = db.StringGet(key);
        ((byte[]?)roundTripped).Should().Equal(payload);
    }

    [Fact]
    public async Task string_set_async_multi_segment_sequence_round_trips()
    {
        await using var conn = Create();
        var db = conn.GetDatabase();
        var key = Me();
        await db.KeyDeleteAsync(key);

        var payload = Encoding.UTF8.GetBytes("a multi-segment sequence payload long enough to span several segments");
        RedisValue value = MultiSegment(payload);
        value.Type.Should().Be(RedisValue.StorageType.Sequence);

        (await db.StringSetAsync(key, value)).Should().BeTrue();

        var roundTripped = await db.StringGetAsync(key);
        ((byte[]?)roundTripped).Should().Equal(payload);
    }
}
