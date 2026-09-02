using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CodeBrix.Redis.Respite.Internal;
using CodeBrix.Redis.Respite.Messages;
using SilverAssertions;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace CodeBrix.Redis.Respite.Tests; //was previously: RESPite.Tests;

public class RespReaderTests(ITestOutputHelper logger)
{
    public readonly struct RespPayload(string label, ReadOnlySequence<byte> payload, byte[] expected, bool? outOfBand, int count)
    {
        public override string ToString() => Label;
        public string Label { get; } = label;
        public ReadOnlySequence<byte> PayloadRaw { get; } = payload;
        public int Length { get; } = CheckPayload(payload, expected, outOfBand, count);
        private static int CheckPayload(scoped in ReadOnlySequence<byte> actual, byte[] expected, bool? outOfBand, int count)
        {
            actual.Length.Should().Be(expected.LongLength);
            var pool = ArrayPool<byte>.Shared.Rent(expected.Length);
            actual.CopyTo(pool);
            bool isSame = pool.AsSpan(0, expected.Length).SequenceEqual(expected);
            ArrayPool<byte>.Shared.Return(pool);
            isSame.Should().BeTrue("the payload should round-trip unchanged");

            // verify that the data exactly passes frame-scanning
            long totalBytes = 0;
            RespReader reader = new(actual);
            while (count > 0)
            {
                RespScanState state = default;
                state.TryRead(ref reader, out long bytesRead).Should().BeTrue();
                totalBytes += bytesRead;
                state.IsComplete.Should().BeTrue(nameof(state.IsComplete));
                if (outOfBand.HasValue)
                {
                    if (outOfBand.Value)
                    {
                        state.Prefix.Should().Be(RespPrefix.Push);
                    }
                    else
                    {
                        state.Prefix.Should().NotBe(RespPrefix.Push);
                    }
                }
                count--;
            }
            totalBytes.Should().Be(expected.Length);
            reader.DemandEnd();
            return expected.Length;
        }

        public RespReader Reader() => new(PayloadRaw);
    }

    public sealed class RespAttribute : DataAttribute
    {
        public override bool SupportsDiscoveryEnumeration() => true;

        private readonly object _value;
        public bool OutOfBand { get; set; } = false;

        private bool? EffectiveOutOfBand => Count == 1 ? OutOfBand : default(bool?);
        public int Count { get; set; } = 1;

        public RespAttribute(string value) => _value = value;
        public RespAttribute(params string[] values) => _value = values;

        public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
            => new(GetData(testMethod).ToArray());

        public IEnumerable<ITheoryDataRow> GetData(MethodInfo testMethod)
        {
            switch (_value)
            {
                case string s:
                    foreach (var item in GetVariants(s, EffectiveOutOfBand, Count))
                    {
                        yield return new TheoryDataRow<RespPayload>(item);
                    }
                    break;
                case string[] arr:
                    foreach (string s in arr)
                    {
                        foreach (var item in GetVariants(s, EffectiveOutOfBand, Count))
                        {
                            yield return new TheoryDataRow<RespPayload>(item);
                        }
                    }
                    break;
            }
        }

        private static IEnumerable<RespPayload> GetVariants(string value, bool? outOfBand, int count)
        {
            var bytes = Encoding.UTF8.GetBytes(value);

            // all in one
            yield return new("Right-sized", new(bytes), bytes, outOfBand, count);

            var bigger = new byte[bytes.Length + 4];
            bytes.CopyTo(bigger.AsSpan(2, bytes.Length));
            bigger.AsSpan(0, 2).Fill(0xFF);
            bigger.AsSpan(bytes.Length + 2, 2).Fill(0xFF);

            // all in one, oversized
            yield return new("Oversized", new(bigger, 2, bytes.Length), bytes, outOfBand, count);

            // two-chunks
            for (int i = 0; i <= bytes.Length; i++)
            {
                int offset = 2 + i;
                var left = new Segment(new ReadOnlyMemory<byte>(bigger, 0, offset), null);
                var right = new Segment(new ReadOnlyMemory<byte>(bigger, offset, bigger.Length - offset), left);
                yield return new($"Split:{i}", new ReadOnlySequence<byte>(left, 2, right, right.Length - 2), bytes, outOfBand, count);
            }

            // N-chunks
            Segment head = new(new(bytes, 0, 1), null), tail = head;
            for (int i = 1; i < bytes.Length; i++)
            {
                tail = new(new(bytes, i, 1), tail);
            }
            yield return new("Chunk-per-byte", new(head, 0, tail, 1), bytes, outOfBand, count);
        }
    }

    [Theory, Resp("$3\r\n128\r\n")]
    public void handle_split_tokens(RespPayload payload)
    {
        //Arrange
        RespReader reader = payload.Reader();
        RespScanState scan = default;

        //Act
        bool readResult = scan.TryRead(ref reader, out _);
        logger.WriteLine(scan.ToString());

        //Assert
        reader.BytesConsumed.Should().Be(payload.Length);
        readResult.Should().BeTrue();
    }

    // the examples from https://github.com/redis/redis-specifications/blob/master/protocol/RESP3.md
    [Theory, Resp("$11\r\nhello world\r\n", "$?\r\n;6\r\nhello \r\n;5\r\nworld\r\n;0\r\n")]
    public void blob_string(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.BulkString);

        //Assert
        reader.Is("hello world"u8).Should().BeTrue();
        reader.ReadString().Should().Be("hello world");
        reader.ReadString(out var prefix).Should().Be("hello world");
        prefix.Should().Be("");
        reader.ParseChars<string>().Should().Be("hello world");
        /* interestingly, string does not implement IUtf8SpanParsable
        reader.ParseBytes<string>().Should().Be("hello world");
        */
        reader.DemandEnd();
    }

    [Theory, Resp("$0\r\n\r\n", "$?\r\n;0\r\n")]
    public void empty_blob_string(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.BulkString);

        //Assert
        reader.Is(""u8).Should().BeTrue();
        reader.ReadString().Should().Be("");
        reader.DemandEnd();
    }

    [Theory, Resp("+hello world\r\n")]
    public void simple_string(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.SimpleString);

        //Assert
        reader.Is("hello world"u8).Should().BeTrue();
        reader.ReadString().Should().Be("hello world");
        reader.ReadString(out var prefix).Should().Be("hello world");
        prefix.Should().Be("");
        reader.DemandEnd();
    }

    [Theory, Resp("-ERR this is the error description\r\n")]
    public void simple_error_implicit_errors(RespPayload payload)
    {
        //Arrange
        Action act = () =>
        {
            var reader = payload.Reader();
            reader.MoveNext();
        };

        //Act
        var thrown = act.Should().ThrowExactly<RespException>();

        //Assert
        thrown.Which.Message.Should().Be("ERR this is the error description");
    }

    [Theory, Resp("-ERR this is the error description\r\n")]
    public void simple_error_careful(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        var moved = reader.TryMoveNext(checkError: false);

        //Assert
        moved.Should().BeTrue();
        reader.Prefix.Should().Be(RespPrefix.SimpleError);
        reader.Is("ERR this is the error description"u8).Should().BeTrue();
        reader.ReadString().Should().Be("ERR this is the error description");
        reader.DemandEnd();
    }

    [Theory, Resp(":1234\r\n")]
    public void number(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Integer);

        //Assert
        reader.Is("1234"u8).Should().BeTrue();
        reader.ReadString().Should().Be("1234");
        reader.ReadInt32().Should().Be(1234);
        reader.ReadDouble().Should().Be(1234D);
        reader.ReadDecimal().Should().Be(1234M);
        reader.ParseChars<int>().Should().Be(1234);
        reader.ParseChars<double>().Should().Be(1234D);
        reader.ParseChars<decimal>().Should().Be(1234M);
        reader.ParseBytes<int>().Should().Be(1234);
        reader.ParseBytes<double>().Should().Be(1234D);
        reader.ParseBytes<decimal>().Should().Be(1234M);
        reader.DemandEnd();
    }

    [Theory, Resp("_\r\n")]
    public void null_value(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Null);

        //Assert
        reader.Is(""u8).Should().BeTrue();
        reader.ReadString().Should().BeNull();
        reader.DemandEnd();
    }

    [Theory, Resp("$-1\r\n")]
    public void null_string(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.BulkString);

        //Assert
        reader.IsNull.Should().BeTrue();
        reader.ReadString().Should().BeNull();
        reader.ScalarLength().Should().Be(0);
        reader.Is(""u8).Should().BeTrue();
        reader.ScalarIsEmpty().Should().BeTrue();

        var iterator = reader.ScalarChunks();
        iterator.MoveNext().Should().BeFalse();
        iterator.MovePast(out reader);
        reader.DemandEnd();
    }

    [Theory, Resp(",1.23\r\n")]
    public void double_value(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Double);

        //Assert
        reader.Is("1.23"u8).Should().BeTrue();
        reader.ReadString().Should().Be("1.23");
        reader.ReadDouble().Should().Be(1.23D);
        reader.ReadDecimal().Should().Be(1.23M);
        reader.DemandEnd();
    }

    [Theory, Resp(":10\r\n")]
    public void integer_simple(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Integer);

        //Assert
        reader.Is("10"u8).Should().BeTrue();
        reader.ReadString().Should().Be("10");
        reader.ReadInt32().Should().Be(10);
        reader.ReadDouble().Should().Be(10D);
        reader.ReadDecimal().Should().Be(10M);
        reader.DemandEnd();
    }

    [Theory, Resp(",10\r\n")]
    public void double_simple(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Double);

        //Assert
        reader.Is("10"u8).Should().BeTrue();
        reader.ReadString().Should().Be("10");
        reader.ReadInt32().Should().Be(10);
        reader.ReadDouble().Should().Be(10D);
        reader.ReadDecimal().Should().Be(10M);
        reader.DemandEnd();
    }

    [Theory, Resp(",inf\r\n")]
    public void double_infinity(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Double);

        //Assert
        reader.Is("inf"u8).Should().BeTrue();
        reader.ReadString().Should().Be("inf");
        var val = reader.ReadDouble();
        double.IsInfinity(val).Should().BeTrue();
        double.IsPositiveInfinity(val).Should().BeTrue();
        reader.DemandEnd();
    }

    [Theory, Resp(",+inf\r\n")]
    public void double_pos_infinity(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Double);

        //Assert
        reader.Is("+inf"u8).Should().BeTrue();
        reader.ReadString().Should().Be("+inf");
        var val = reader.ReadDouble();
        double.IsInfinity(val).Should().BeTrue();
        double.IsPositiveInfinity(val).Should().BeTrue();
        reader.DemandEnd();
    }

    [Theory, Resp(",-inf\r\n")]
    public void double_neg_infinity(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Double);

        //Assert
        reader.Is("-inf"u8).Should().BeTrue();
        reader.ReadString().Should().Be("-inf");
        var val = reader.ReadDouble();
        double.IsInfinity(val).Should().BeTrue();
        double.IsNegativeInfinity(val).Should().BeTrue();
        reader.DemandEnd();
    }

    [Theory, Resp(",nan\r\n")]
    public void double_nan(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Double);

        //Assert
        reader.Is("nan"u8).Should().BeTrue();
        reader.ReadString().Should().Be("nan");
        var val = reader.ReadDouble();
        double.IsNaN(val).Should().BeTrue();
        reader.DemandEnd();
    }

    [Theory, Resp("#t\r\n")]
    public void boolean_t(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Boolean);

        //Assert
        reader.ReadBoolean().Should().BeTrue();
        reader.DemandEnd();
    }

    [Theory, Resp("#f\r\n")]
    public void boolean_f(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Boolean);

        //Assert
        reader.ReadBoolean().Should().BeFalse();
        reader.DemandEnd();
    }

    [Theory, Resp(":1\r\n")]
    public void boolean_1(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Integer);

        //Assert
        reader.ReadBoolean().Should().BeTrue();
        reader.DemandEnd();
    }

    [Theory, Resp(":0\r\n")]
    public void boolean_0(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Integer);

        //Assert
        reader.ReadBoolean().Should().BeFalse();
        reader.DemandEnd();
    }

    [Theory, Resp("!21\r\nSYNTAX invalid syntax\r\n", "!?\r\n;6\r\nSYNTAX\r\n;15\r\n invalid syntax\r\n;0\r\n")]
    public void blob_error_implicit_errors(RespPayload payload)
    {
        //Arrange
        Action act = () =>
        {
            var reader = payload.Reader();
            reader.MoveNext();
        };

        //Act
        var thrown = act.Should().ThrowExactly<RespException>();

        //Assert
        thrown.Which.Message.Should().Be("SYNTAX invalid syntax");
    }

    [Theory, Resp("!21\r\nSYNTAX invalid syntax\r\n", "!?\r\n;6\r\nSYNTAX\r\n;15\r\n invalid syntax\r\n;0\r\n")]
    public void blob_error_careful(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        var moved = reader.TryMoveNext(checkError: false);

        //Assert
        moved.Should().BeTrue();
        reader.Prefix.Should().Be(RespPrefix.BulkError);
        reader.Is("SYNTAX invalid syntax"u8).Should().BeTrue();
        reader.ReadString().Should().Be("SYNTAX invalid syntax");
        reader.DemandEnd();
    }

    [Theory, Resp("=15\r\ntxt:Some string\r\n", "=?\r\n;4\r\ntxt:\r\n;11\r\nSome string\r\n;0\r\n")]
    public void verbatim_string(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.VerbatimString);

        //Assert
        reader.ReadString().Should().Be("Some string");
        reader.ReadString(out var prefix).Should().Be("Some string");
        prefix.Should().Be("txt");

        reader.ReadString(out var prefix2).Should().Be("Some string");
        prefix2.Should().BeSameAs(prefix); // check prefix recognized and reuse literal
        reader.DemandEnd();
    }

    [Theory, Resp("(3492890328409238509324850943850943825024385\r\n")]
    public void big_integers(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.BigInteger);

        //Assert
        reader.ReadString().Should().Be("3492890328409238509324850943850943825024385");
        var actual = reader.ParseChars(chars => BigInteger.Parse(chars, CultureInfo.InvariantCulture));

        var expected = BigInteger.Parse("3492890328409238509324850943850943825024385");
        actual.Should().Be(expected);
    }

    // The foreach below drives AggregateEnumerator through the enumerator pattern deliberately, which reads
    // .Current - and a DEBUG build of the library gates .Current behind [Experimental("SERDBG")] to nudge
    // library code towards .Value. Upstream brackets exactly this foreach with a #pragma warning
    // disable/restore SERDBG; this repository carries no pragmas, and SERDBG is NOT one of the six SER
    // identifiers on the csproj NoWarn line (that list is closed), so the opt-in is taken the way the
    // language intends: on the one member that needs it, with the same diagnostic ID upstream names.
    [Experimental("SERDBG")]
    [Theory, Resp("*3\r\n:1\r\n:2\r\n:3\r\n", "*?\r\n:1\r\n:2\r\n:3\r\n.\r\n")]
    public void array_value(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Array);

        //Assert
        reader.AggregateLength().Should().Be(3);
        var iterator = reader.AggregateChildren();
        iterator.MoveNext(RespPrefix.Integer).Should().BeTrue();
        iterator.Value.ReadInt32().Should().Be(1);
        iterator.Value.DemandEnd();

        iterator.MoveNext(RespPrefix.Integer).Should().BeTrue();
        iterator.Value.ReadInt32().Should().Be(2);
        iterator.Value.DemandEnd();

        iterator.MoveNext(RespPrefix.Integer).Should().BeTrue();
        iterator.Value.ReadInt32().Should().Be(3);
        iterator.Value.DemandEnd();

        iterator.MoveNext(RespPrefix.Integer).Should().BeFalse();
        iterator.MovePast(out reader);
        reader.DemandEnd();

        reader = payload.Reader();
        reader.MoveNext(RespPrefix.Array);
        int[] arr = new int[reader.AggregateLength()];
        int i = 0;
        foreach (var sub in reader.AggregateChildren())
        {
            sub.Demand(RespPrefix.Integer);
            arr[i++] = sub.ReadInt32();
            sub.DemandEnd();
        }
        iterator.MovePast(out reader);
        reader.DemandEnd();

        arr.Should().Equal([1, 2, 3]);
    }

    [Theory, Resp("*-1\r\n")]
    public void null_array(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Array);

        //Assert
        reader.IsNull.Should().BeTrue();
        reader.AggregateLength().Should().Be(0);
        var iterator = reader.AggregateChildren();
        iterator.MoveNext().Should().BeFalse();
        iterator.MovePast(out reader);
        reader.DemandEnd();
    }

    [Theory, Resp("*2\r\n*3\r\n:1\r\n$5\r\nhello\r\n:2\r\n#f\r\n", "*?\r\n*?\r\n:1\r\n$5\r\nhello\r\n:2\r\n.\r\n#f\r\n.\r\n")]
    public void nested_array(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Array);

        //Assert
        reader.AggregateLength().Should().Be(2);

        var iterator = reader.AggregateChildren();
        iterator.MoveNext(RespPrefix.Array).Should().BeTrue();

        iterator.Value.AggregateLength().Should().Be(3);
        var subIterator = iterator.Value.AggregateChildren();
        subIterator.MoveNext(RespPrefix.Integer).Should().BeTrue();
        subIterator.Value.ReadInt64().Should().Be(1);
        subIterator.Value.DemandEnd();

        subIterator.MoveNext(RespPrefix.BulkString).Should().BeTrue();
        subIterator.Value.Is("hello"u8).Should().BeTrue();
        subIterator.Value.DemandEnd();

        subIterator.MoveNext(RespPrefix.Integer).Should().BeTrue();
        subIterator.Value.ReadInt64().Should().Be(2);
        subIterator.Value.DemandEnd();

        subIterator.MoveNext().Should().BeFalse();

        iterator.MoveNext(RespPrefix.Boolean).Should().BeTrue();
        iterator.Value.ReadBoolean().Should().BeFalse();
        iterator.Value.DemandEnd();

        iterator.MoveNext().Should().BeFalse();
        iterator.MovePast(out reader);

        reader.DemandEnd();
    }

    [Theory, Resp("%2\r\n+first\r\n:1\r\n+second\r\n:2\r\n", "%?\r\n+first\r\n:1\r\n+second\r\n:2\r\n.\r\n")]
    public void map(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Map);

        //Assert
        reader.AggregateLength().Should().Be(4);

        var iterator = reader.AggregateChildren();

        iterator.MoveNext(RespPrefix.SimpleString).Should().BeTrue();
        iterator.Value.Is("first".AsSpan()).Should().BeTrue();
        iterator.Value.DemandEnd();

        iterator.MoveNext(RespPrefix.Integer).Should().BeTrue();
        iterator.Value.ReadInt32().Should().Be(1);
        iterator.Value.DemandEnd();

        iterator.MoveNext(RespPrefix.SimpleString).Should().BeTrue();
        iterator.Value.Is("second"u8).Should().BeTrue();
        iterator.Value.DemandEnd();

        iterator.MoveNext(RespPrefix.Integer).Should().BeTrue();
        iterator.Value.ReadInt32().Should().Be(2);
        iterator.Value.DemandEnd();

        iterator.MoveNext().Should().BeFalse();

        iterator.MovePast(out reader);
        reader.DemandEnd();
    }

    [Theory, Resp("~5\r\n+orange\r\n+apple\r\n#t\r\n:100\r\n:999\r\n", "~?\r\n+orange\r\n+apple\r\n#t\r\n:100\r\n:999\r\n.\r\n")]
    public void set(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Set);

        //Assert
        reader.AggregateLength().Should().Be(5);

        var iterator = reader.AggregateChildren();

        iterator.MoveNext(RespPrefix.SimpleString).Should().BeTrue();
        iterator.Value.Is("orange".AsSpan()).Should().BeTrue();
        iterator.Value.DemandEnd();

        iterator.MoveNext(RespPrefix.SimpleString).Should().BeTrue();
        iterator.Value.Is("apple"u8).Should().BeTrue();
        iterator.Value.DemandEnd();

        iterator.MoveNext(RespPrefix.Boolean).Should().BeTrue();
        iterator.Value.ReadBoolean().Should().BeTrue();
        iterator.Value.DemandEnd();

        iterator.MoveNext(RespPrefix.Integer).Should().BeTrue();
        iterator.Value.ReadInt32().Should().Be(100);
        iterator.Value.DemandEnd();

        iterator.MoveNext(RespPrefix.Integer).Should().BeTrue();
        iterator.Value.ReadInt32().Should().Be(999);
        iterator.Value.DemandEnd();

        iterator.MoveNext().Should().BeFalse();

        iterator.MovePast(out reader);
        reader.DemandEnd();
    }

    private sealed class TestAttributeReader : RespAttributeReader<(int Count, int Ttl, decimal A, decimal B)>
    {
        public override void Read(ref RespReader reader, ref (int Count, int Ttl, decimal A, decimal B) value)
        {
            value.Count += ReadKeyValuePairs(ref reader, ref value);
        }
        private TestAttributeReader() { }
        public static readonly TestAttributeReader Instance = new();
        public static (int Count, int Ttl, decimal A, decimal B) Zero = (0, 0, 0, 0);
        public override bool ReadKeyValuePair(scoped ReadOnlySpan<byte> key, ref RespReader reader, ref (int Count, int Ttl, decimal A, decimal B) value)
        {
            if (key.SequenceEqual("ttl"u8) && reader.IsScalar)
            {
                value.Ttl = reader.ReadInt32();
            }
            else if (key.SequenceEqual("key-popularity"u8) && reader.IsAggregate)
            {
                ReadKeyValuePairs(ref reader, ref value); // recurse to process a/b below
            }
            else if (key.SequenceEqual("a"u8) && reader.IsScalar)
            {
                value.A = reader.ReadDecimal();
            }
            else if (key.SequenceEqual("b"u8) && reader.IsScalar)
            {
                value.B = reader.ReadDecimal();
            }
            else
            {
                return false; // not recognized
            }
            return true; // recognized
        }
    }

    [Theory, Resp(
        "|1\r\n+key-popularity\r\n%2\r\n$1\r\na\r\n,0.1923\r\n$1\r\nb\r\n,0.0012\r\n*2\r\n:2039123\r\n:9543892\r\n",
        "|1\r\n+key-popularity\r\n%2\r\n$1\r\na\r\n,0.1923\r\n$1\r\nb\r\n,0.0012\r\n*?\r\n:2039123\r\n:9543892\r\n.\r\n")]
    public void attribute_root(RespPayload payload)
    {
        //Arrange
        // ignore the attribute data
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Array);

        //Assert
        reader.AggregateLength().Should().Be(2);
        var iterator = reader.AggregateChildren();

        iterator.MoveNext(RespPrefix.Integer).Should().BeTrue();
        iterator.Value.ReadInt32().Should().Be(2039123);
        iterator.Value.DemandEnd();

        iterator.MoveNext(RespPrefix.Integer).Should().BeTrue();
        iterator.Value.ReadInt32().Should().Be(9543892);
        iterator.Value.DemandEnd();

        iterator.MoveNext().Should().BeFalse();
        iterator.MovePast(out reader);
        reader.DemandEnd();

        // process the attribute data
        var state = TestAttributeReader.Zero;
        reader = payload.Reader();
        reader.MoveNext(RespPrefix.Array, TestAttributeReader.Instance, ref state);
        state.Count.Should().Be(1);
        state.A.Should().Be(0.1923M);
        state.B.Should().Be(0.0012M);
        state = TestAttributeReader.Zero;

        reader.AggregateLength().Should().Be(2);
        iterator = reader.AggregateChildren();

        iterator.MoveNext(RespPrefix.Integer, TestAttributeReader.Instance, ref state).Should().BeTrue();
        iterator.Value.ReadInt32().Should().Be(2039123);
        state.Count.Should().Be(0);
        iterator.Value.DemandEnd();

        iterator.MoveNext(RespPrefix.Integer, TestAttributeReader.Instance, ref state).Should().BeTrue();
        iterator.Value.ReadInt32().Should().Be(9543892);
        state.Count.Should().Be(0);
        iterator.Value.DemandEnd();

        iterator.MoveNext().Should().BeFalse();
        iterator.MovePast(out reader);
        reader.DemandEnd();
    }

    [Theory, Resp("*3\r\n:1\r\n:2\r\n|1\r\n+ttl\r\n:3600\r\n:3\r\n", "*?\r\n:1\r\n:2\r\n|1\r\n+ttl\r\n:3600\r\n:3\r\n.\r\n")]
    public void attribute_inner(RespPayload payload)
    {
        //Arrange
        // ignore the attribute data
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Array);

        //Assert
        reader.AggregateLength().Should().Be(3);
        var iterator = reader.AggregateChildren();

        iterator.MoveNext(RespPrefix.Integer).Should().BeTrue();
        iterator.Value.ReadInt32().Should().Be(1);
        iterator.Value.DemandEnd();

        iterator.MoveNext(RespPrefix.Integer).Should().BeTrue();
        iterator.Value.ReadInt32().Should().Be(2);
        iterator.Value.DemandEnd();

        iterator.MoveNext(RespPrefix.Integer).Should().BeTrue();
        iterator.Value.ReadInt32().Should().Be(3);
        iterator.Value.DemandEnd();

        iterator.MoveNext().Should().BeFalse();
        iterator.MovePast(out reader);
        reader.DemandEnd();

        // process the attribute data
        var state = TestAttributeReader.Zero;
        reader = payload.Reader();
        reader.MoveNext(RespPrefix.Array, TestAttributeReader.Instance, ref state);
        state.Count.Should().Be(0);
        reader.AggregateLength().Should().Be(3);
        iterator = reader.AggregateChildren();

        iterator.MoveNext(RespPrefix.Integer, TestAttributeReader.Instance, ref state).Should().BeTrue();
        state.Count.Should().Be(0);
        iterator.Value.ReadInt32().Should().Be(1);
        iterator.Value.DemandEnd();

        iterator.MoveNext(RespPrefix.Integer, TestAttributeReader.Instance, ref state).Should().BeTrue();
        state.Count.Should().Be(0);
        iterator.Value.ReadInt32().Should().Be(2);
        iterator.Value.DemandEnd();

        iterator.MoveNext(RespPrefix.Integer, TestAttributeReader.Instance, ref state).Should().BeTrue();
        state.Count.Should().Be(1);
        state.Ttl.Should().Be(3600);
        state = TestAttributeReader.Zero; // reset
        iterator.Value.ReadInt32().Should().Be(3);
        iterator.Value.DemandEnd();

        iterator.MoveNextRaw(TestAttributeReader.Instance, ref state).Should().BeFalse();
        state.Count.Should().Be(0);
        iterator.MovePast(out reader);
        reader.DemandEnd();
    }

    [Theory, Resp(">3\r\n+message\r\n+somechannel\r\n+this is the message\r\n", OutOfBand = true)]
    public void push(RespPayload payload)
    {
        //Arrange
        // ignore the attribute data
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Push);

        //Assert
        reader.AggregateLength().Should().Be(3);
        var iterator = reader.AggregateChildren();

        iterator.MoveNext(RespPrefix.SimpleString).Should().BeTrue();
        iterator.Value.Is("message"u8).Should().BeTrue();
        iterator.Value.DemandEnd();

        iterator.MoveNext(RespPrefix.SimpleString).Should().BeTrue();
        iterator.Value.Is("somechannel"u8).Should().BeTrue();
        iterator.Value.DemandEnd();

        iterator.MoveNext(RespPrefix.SimpleString).Should().BeTrue();
        iterator.Value.Is("this is the message"u8).Should().BeTrue();
        iterator.Value.DemandEnd();

        iterator.MoveNext().Should().BeFalse();
        iterator.MovePast(out reader);
        reader.DemandEnd();
    }

    [Theory, Resp(">3\r\n+message\r\n+somechannel\r\n+this is the message\r\n$9\r\nGet-Reply\r\n", Count = 2)]
    public void push_then_get_reply(RespPayload payload)
    {
        //Arrange
        // ignore the attribute data
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Push);

        //Assert
        reader.AggregateLength().Should().Be(3);
        var iterator = reader.AggregateChildren();

        iterator.MoveNext(RespPrefix.SimpleString).Should().BeTrue();
        iterator.Value.Is("message"u8).Should().BeTrue();
        iterator.Value.DemandEnd();

        iterator.MoveNext(RespPrefix.SimpleString).Should().BeTrue();
        iterator.Value.Is("somechannel"u8).Should().BeTrue();
        iterator.Value.DemandEnd();

        iterator.MoveNext(RespPrefix.SimpleString).Should().BeTrue();
        iterator.Value.Is("this is the message"u8).Should().BeTrue();
        iterator.Value.DemandEnd();

        iterator.MoveNext().Should().BeFalse();
        iterator.MovePast(out reader);

        reader.MoveNext(RespPrefix.BulkString);
        reader.Is("Get-Reply"u8).Should().BeTrue();
        reader.DemandEnd();
    }

    [Theory, Resp("$9\r\nGet-Reply\r\n>3\r\n+message\r\n+somechannel\r\n+this is the message\r\n", Count = 2)]
    public void get_reply_then_push(RespPayload payload)
    {
        //Arrange
        // ignore the attribute data
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.BulkString);

        //Assert
        reader.Is("Get-Reply"u8).Should().BeTrue();

        reader.MoveNext(RespPrefix.Push);
        reader.AggregateLength().Should().Be(3);
        var iterator = reader.AggregateChildren();

        iterator.MoveNext(RespPrefix.SimpleString).Should().BeTrue();
        iterator.Value.Is("message"u8).Should().BeTrue();
        iterator.Value.DemandEnd();

        iterator.MoveNext(RespPrefix.SimpleString).Should().BeTrue();
        iterator.Value.Is("somechannel"u8).Should().BeTrue();
        iterator.Value.DemandEnd();

        iterator.MoveNext(RespPrefix.SimpleString).Should().BeTrue();
        iterator.Value.Is("this is the message"u8).Should().BeTrue();
        iterator.Value.DemandEnd();

        iterator.MoveNext().Should().BeFalse();
        iterator.MovePast(out reader);

        reader.DemandEnd();
    }

    [Theory, Resp("*0\r\n$4\r\npass\r\n", "*1\r\n+ok\r\n$4\r\npass\r\n", "*-1\r\n$4\r\npass\r\n", "*?\r\n.\r\n$4\r\npass\r\n", Count = 2)]
    public void array_then_string(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        var moved = reader.TryMoveNext(RespPrefix.Array);

        //Assert
        moved.Should().BeTrue();
        reader.SkipChildren();

        reader.TryMoveNext(RespPrefix.BulkString).Should().BeTrue();
        reader.Is("pass"u8).Should().BeTrue();

        reader.DemandEnd();

        // and the same using child iterator
        reader = payload.Reader();
        reader.TryMoveNext(RespPrefix.Array).Should().BeTrue();
        var iterator = reader.AggregateChildren();
        iterator.MovePast(out reader);

        reader.TryMoveNext(RespPrefix.BulkString).Should().BeTrue();
        reader.Is("pass"u8).Should().BeTrue();

        reader.DemandEnd();
    }

    // Tests for ScalarLengthIs
    [Theory, Resp("$-1\r\n")] // null bulk string
    public void scalar_length_is_null_bulk_string(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.BulkString);

        //Assert
        reader.ScalarLengthIs(0).Should().BeTrue();
        reader.ScalarLengthIs(1).Should().BeFalse();
        reader.ScalarLengthIs(5).Should().BeFalse();
        reader.DemandEnd();
    }

    // Note: Null prefix (_\r\n) is tested in the existing null_value() test above
    [Theory, Resp("$0\r\n\r\n", "$?\r\n;0\r\n")] // empty scalar (simple and streaming)
    public void scalar_length_is_empty(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.BulkString);

        //Assert
        reader.ScalarLengthIs(0).Should().BeTrue();
        reader.ScalarLengthIs(1).Should().BeFalse();
        reader.ScalarLengthIs(5).Should().BeFalse();
        reader.DemandEnd();
    }

    [Theory, Resp("$5\r\nhello\r\n")] // simple scalar
    public void scalar_length_is_simple(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.BulkString);

        //Assert
        reader.ScalarLengthIs(5).Should().BeTrue();
        reader.ScalarLengthIs(0).Should().BeFalse();
        reader.ScalarLengthIs(4).Should().BeFalse();
        reader.ScalarLengthIs(6).Should().BeFalse();
        reader.ScalarLengthIs(10).Should().BeFalse();
        reader.DemandEnd();
    }

    [Theory, Resp("$?\r\n;2\r\nhe\r\n;3\r\nllo\r\n;0\r\n")] // streaming scalar
    public void scalar_length_is_streaming(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.BulkString);

        //Assert
        reader.ScalarLengthIs(5).Should().BeTrue();
        reader.ScalarLengthIs(0).Should().BeFalse();
        reader.ScalarLengthIs(2).Should().BeFalse(); // short-circuit: stops early
        reader.ScalarLengthIs(3).Should().BeFalse(); // short-circuit: stops early
        reader.ScalarLengthIs(6).Should().BeFalse(); // short-circuit: stops early
        reader.ScalarLengthIs(10).Should().BeFalse(); // short-circuit: stops early
        reader.DemandEnd();
    }

    [Fact] // streaming scalar - verify short-circuiting stops before reading malformed data
    public void scalar_length_is_streaming_short_circuits()
    {
        //Arrange
        // Streaming scalar: 2 bytes "he", then 3 bytes "llo", then 1 byte "X", then MALFORMED
        // To check if length == N, we need to read N+1 bytes to verify there isn't more
        // So malformed data must come AFTER the N+1 threshold
        var data = "$?\r\n;2\r\nhe\r\n;3\r\nllo\r\n;1\r\nX\r\nMALFORMED"u8.ToArray();
        var reader = new RespReader(new ReadOnlySequence<byte>(data));

        //Act
        reader.MoveNext(RespPrefix.BulkString);

        //Assert
        // When checking length < 6, we read up to 6 bytes (he+llo+X), see 6 > expected, stop
        reader.ScalarLengthIs(0).Should().BeFalse(); // reads "he" (2), 2 > 0, stops before "llo"
        reader.ScalarLengthIs(2).Should().BeFalse(); // reads "he" (2), "llo" (5 total), 5 > 2, stops before "X"
        reader.ScalarLengthIs(4).Should().BeFalse(); // reads "he" (2), "llo" (5 total), 5 > 4, stops before "X"
        reader.ScalarLengthIs(5).Should().BeFalse(); // reads "he" (2), "llo" (5), "X" (6 total), 6 > 5, stops before MALFORMED

        // All of the above should succeed without hitting MALFORMED because we short-circuit
    }

    [Fact] // streaming scalar - verify TryGetSpan fails and Buffer works correctly
    public void streaming_scalar_buffer_partial()
    {
        //Arrange
        // 32 bytes total: "abcdefgh" (8) + "ijklmnop" (8) + "qrstuvwx" (8) + "yz012345" (8) + "6789" (4)
        var data = "$?\r\n;8\r\nabcdefgh\r\n;8\r\nijklmnop\r\n;8\r\nqrstuvwx\r\n;8\r\nyz012345\r\n;4\r\n6789\r\n;0\r\n"u8.ToArray();
        var reader = new RespReader(new ReadOnlySequence<byte>(data));

        //Act
        reader.MoveNext(RespPrefix.BulkString);

        //Assert
        reader.IsScalar.Should().BeTrue();
        reader.TryGetSpan(out _).Should().BeFalse(); // Should fail - data is non-contiguous

        // Buffer should fetch just the first 16 bytes
        Span<byte> buffer = stackalloc byte[16];
        var buffered = reader.Buffer(buffer);
        buffered.Length.Should().Be(16);
        buffered.SequenceEqual("abcdefghijklmnop"u8).Should().BeTrue();
    }

    [Theory, Resp("+hello\r\n")] // simple string
    public void scalar_length_is_simple_string(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.SimpleString);

        //Assert
        reader.ScalarLengthIs(5).Should().BeTrue();
        reader.ScalarLengthIs(0).Should().BeFalse();
        reader.ScalarLengthIs(4).Should().BeFalse();
        reader.DemandEnd();
    }

    // Tests for AggregateLengthIs
    [Theory, Resp("*-1\r\n")] // null array
    public void aggregate_length_is_null_array(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Array);

        //Assert
        reader.IsNull.Should().BeTrue();
        // Note: AggregateLength() would throw on null, but AggregateLengthIs should handle it
        reader.DemandEnd();
    }

    [Theory, Resp("*0\r\n", "*?\r\n.\r\n")] // empty array (simple and streaming)
    public void aggregate_length_is_empty(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Array);

        //Assert
        reader.AggregateLengthIs(0).Should().BeTrue();
        reader.AggregateLengthIs(1).Should().BeFalse();
        reader.AggregateLengthIs(3).Should().BeFalse();
        reader.SkipChildren();
        reader.DemandEnd();
    }

    [Theory, Resp("*3\r\n:1\r\n:2\r\n:3\r\n")] // simple array
    public void aggregate_length_is_simple(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Array);

        //Assert
        reader.AggregateLengthIs(3).Should().BeTrue();
        reader.AggregateLengthIs(0).Should().BeFalse();
        reader.AggregateLengthIs(2).Should().BeFalse();
        reader.AggregateLengthIs(4).Should().BeFalse();
        reader.AggregateLengthIs(10).Should().BeFalse();
        reader.SkipChildren();
        reader.DemandEnd();
    }

    [Theory, Resp("*?\r\n:1\r\n:2\r\n:3\r\n.\r\n")] // streaming array
    public void aggregate_length_is_streaming(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Array);

        //Assert
        reader.AggregateLengthIs(3).Should().BeTrue();
        reader.AggregateLengthIs(0).Should().BeFalse();
        reader.AggregateLengthIs(2).Should().BeFalse(); // short-circuit: stops early
        reader.AggregateLengthIs(4).Should().BeFalse(); // short-circuit: stops early
        reader.AggregateLengthIs(10).Should().BeFalse(); // short-circuit: stops early
        reader.SkipChildren();
        reader.DemandEnd();
    }

    [Fact] // streaming array - verify short-circuiting works even with extra data present
    public void aggregate_length_is_streaming_short_circuits()
    {
        //Arrange
        // Streaming array: 3 elements (:1, :2, :3), then extra elements
        // Short-circuiting means we can return false without reading all elements
        var data = "*?\r\n:1\r\n:2\r\n:3\r\n:999\r\n:888\r\n.\r\n"u8.ToArray();
        var reader = new RespReader(new ReadOnlySequence<byte>(data));

        //Act
        reader.MoveNext(RespPrefix.Array);

        //Assert
        // These should all return false via short-circuiting
        // (we know the answer before reading all elements)
        reader.AggregateLengthIs(0).Should().BeFalse(); // can tell after 1 element
        reader.AggregateLengthIs(2).Should().BeFalse(); // can tell after 3 elements
        reader.AggregateLengthIs(4).Should().BeFalse(); // can tell after 4 elements (count > expected)
        reader.AggregateLengthIs(10).Should().BeFalse(); // can tell after 4 elements (count > expected)

        // The actual length is 5 (:1, :2, :3, :999, :888)
        reader.AggregateLengthIs(5).Should().BeTrue();
    }

    [Fact] // streaming array - verify short-circuiting stops before reading malformed data
    public void aggregate_length_is_streaming_malformed_after_short_circuit()
    {
        //Arrange
        // Streaming array: 3 elements (:1, :2, :3), then :4, then MALFORMED
        // To check if length == N, we need to read N+1 elements to verify there isn't more
        // So malformed data must come AFTER the N+1 threshold
        var data = "*?\r\n:1\r\n:2\r\n:3\r\n:4\r\nGARBAGE_NOT_A_VALID_ELEMENT"u8.ToArray();
        var reader = new RespReader(new ReadOnlySequence<byte>(data));

        //Act
        reader.MoveNext(RespPrefix.Array);

        //Assert
        // When checking length < 4, we read up to 4 elements, see 4 > expected, stop
        reader.AggregateLengthIs(0).Should().BeFalse(); // reads :1 (1 element), 1 > 0, stops before :2
        reader.AggregateLengthIs(2).Should().BeFalse(); // reads :1, :2, :3 (3 elements), 3 > 2, stops before :4
        reader.AggregateLengthIs(3).Should().BeFalse(); // reads :1, :2, :3, :4 (4 elements), 4 > 3, stops before MALFORMED

        // All of the above should succeed without hitting MALFORMED because we short-circuit
    }

    [Theory, Resp("%2\r\n+first\r\n:1\r\n+second\r\n:2\r\n", "%?\r\n+first\r\n:1\r\n+second\r\n:2\r\n.\r\n")] // map (simple and streaming)
    public void aggregate_length_is_map(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Map);

        //Assert
        // Map length is doubled (2 pairs = 4 elements)
        reader.AggregateLengthIs(4).Should().BeTrue();
        reader.AggregateLengthIs(0).Should().BeFalse();
        reader.AggregateLengthIs(2).Should().BeFalse();
        reader.AggregateLengthIs(3).Should().BeFalse();
        reader.AggregateLengthIs(5).Should().BeFalse();
        reader.SkipChildren();
        reader.DemandEnd();
    }

    [Theory, Resp("~5\r\n+orange\r\n+apple\r\n#t\r\n:100\r\n:999\r\n", "~?\r\n+orange\r\n+apple\r\n#t\r\n:100\r\n:999\r\n.\r\n")] // set (simple and streaming)
    public void aggregate_length_is_set(RespPayload payload)
    {
        //Arrange
        var reader = payload.Reader();

        //Act
        reader.MoveNext(RespPrefix.Set);

        //Assert
        reader.AggregateLengthIs(5).Should().BeTrue();
        reader.AggregateLengthIs(0).Should().BeFalse();
        reader.AggregateLengthIs(4).Should().BeFalse();
        reader.AggregateLengthIs(6).Should().BeFalse();
        reader.SkipChildren();
        reader.DemandEnd();
    }

    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        public override string ToString() => RespConstants.UTF8.GetString(Memory.Span)
            .Replace("\r", "\\r").Replace("\n", "\\n");

        public Segment(ReadOnlyMemory<byte> value, Segment? head)
        {
            Memory = value;
            if (head is not null)
            {
                RunningIndex = head.RunningIndex + head.Memory.Length;
                head.Next = this;
            }
        }
        public bool IsEmpty => Memory.IsEmpty;
        public int Length => Memory.Length;
    }
}
