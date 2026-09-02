using System;
using System.Threading.Tasks;
using CodeBrix.Redis.TestServer;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public sealed class VectorSetUnitTests(ITestOutputHelper output)
{
    // the aim of this test is to validate that we're sending the right thing - VADD is complex
    [Theory]
    [InlineData(VectorSetQuantization.Int8, false)]
    [InlineData(VectorSetQuantization.None, false)]
    [InlineData(VectorSetQuantization.Binary, false)]
    [InlineData(VectorSetQuantization.Int8, true)]
    [InlineData(VectorSetQuantization.None, true)]
    [InlineData(VectorSetQuantization.Binary, true)]
    public async Task vector_set_add_with_everything(VectorSetQuantization quantization, bool useFp32)
    {
        using var server = new VectorServer(output);
        await using var conn = await server.ConnectAsync();
        var db = conn.GetDatabase();
        var key = "mykey";

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector = new[] { 1.0f, 2.0f, 3.0f, 4.0f };
        var attributes = """{"category":"test","id":123}""";

        var request = VectorSetAddRequest.Member(
            "element1",
            vector.AsMemory(),
            attributes);
        request.UseFp32 = useFp32;
        request.Quantization = quantization;
        request.ReducedDimensions = 4;
        request.BuildExplorationFactor = 300;
        request.MaxConnections = 32;
        request.UseCheckAndSet = true;
        output.WriteLine("Storing...");
        var result = await db.VectorSetAddAsync(
            key,
            request);
        result.Should().BeTrue();

        // now: what did we send?
        var req = server.LastRequest.ReadRequest().AsSpan();

        output.WriteLine($"Request: * {req.Length}");
        foreach (var item in req)
        {
            output.WriteLine($"  $ '{item}'");
        }

        req[0].Should().Be("VADD");
        req[1].Should().Be("mykey");
        req[2].Should().Be("REDUCE");
        req[3].Should().Be(4);
        req = req.Slice(4);

        if (useFp32)
        {
            req[0].Should().Be("FP32");
            BitConverter.ToString(req[1]!).Should().Be("00-00-80-3F-00-00-00-40-00-00-40-40-00-00-80-40");
            req = req.Slice(2);
        }
        else
        {
            req[0].Should().Be("VALUES");
            req[1].Should().Be(4);
            ((float)req[2]).Should().BeApproximately(1.0f, 0.001f);
            ((float)req[3]).Should().BeApproximately(2.0f, 0.001f);
            ((float)req[4]).Should().BeApproximately(3.0f, 0.001f);
            ((float)req[5]).Should().BeApproximately(4.0f, 0.001f);
            req = req.Slice(6);
        }

        req[0].Should().Be("element1");
        req[1].Should().Be("CAS");
        req = req.Slice(2);

        switch (quantization)
        {
            case VectorSetQuantization.None:
                req[0].Should().Be("NOQUANT");
                req = req.Slice(1);
                break;
            case VectorSetQuantization.Binary:
                req[0].Should().Be("BIN");
                req = req.Slice(1);
                break;
        }

        req[0].Should().Be("EF");
        req[1].Should().Be(300);
        req[2].Should().Be("SETATTR");
        req[3].Should().Be("""{"category":"test","id":123}""");
        req[4].Should().Be("M");
        req[5].Should().Be(32);
        req = req.Slice(6);

        req.IsEmpty.Should().BeTrue();
    }

    private sealed class VectorServer(ITestOutputHelper log) : InProcessTestServer(log)
    {
        public TypedRedisValue LastRequest { get; private set; } = TypedRedisValue.Nil;

        [RedisCommand(-1)]
        private TypedRedisValue Vadd(RedisClient client, in RedisRequest request)
        {
            LastRequest = request.AsResponse();
            return TypedRedisValue.Integer(1); // spoof success
        }
    }
}
