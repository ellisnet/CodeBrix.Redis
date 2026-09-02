using System;
using System.Linq;
using System.Text;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

/// <summary>
/// Validates the inline "short blob" storage kind (<see cref="RedisValue.StorageType.ShortBlob"/>), which
/// packs 1..8 payload bytes into the overlapped int64 field instead of allocating a byte[]. Every projection
/// must be indistinguishable from the equivalent <c>byte[]</c>-backed value.
/// </summary>
public class RedisValueShortBlobTests
{
    private static RedisValue Short(byte[] bytes) => RedisValue.FromRaw(bytes); // <= 8 bytes => ShortBlob

    private static RedisValue Sequence(byte[] bytes) // multi-segment, one byte per chunk
        => FragmentedSegment<byte>.Create(bytes.Select((_, i) => new ReadOnlyMemory<byte>(bytes, i, 1)).ToArray());

    [Theory]
    [InlineData("a")]
    [InlineData("ab")]
    [InlineData("OK")]
    [InlineData("hello")]
    [InlineData("12345678")] // 8 bytes, the max inline size
    [InlineData("1234")] // numeric-looking
    [InlineData("-42")]
    [InlineData("0.5")]
    [InlineData("inf")] // special token (not simplified to a double)
    [InlineData("a1b2c3")] // mixed alphanumeric
    public void short_blob_is_indistinguishable_from_byte_array(string text)
    {
        //Arrange
        var bytes = Encoding.UTF8.GetBytes(text);
        (bytes.Length is >= 1 and <= 8).Should().BeTrue();
        var shortBlob = Short(bytes);
        RedisValue byteArray = (byte[])bytes.Clone();
        shortBlob.Type.Should().Be(RedisValue.StorageType.ShortBlob);
        byteArray.Type.Should().Be(RedisValue.StorageType.ByteArray);
        // length / projections
        shortBlob.Length().Should().Be(byteArray.Length());
        ((string?)shortBlob).Should().Be((string?)byteArray);
        ((byte[]?)shortBlob).Should().Equal((byte[]?)byteArray);
        // equality + hash + compare, both directions, against the byte[] form
        (shortBlob == byteArray).Should().BeTrue();
        (byteArray == shortBlob).Should().BeTrue();
        shortBlob.Equals(byteArray).Should().BeTrue();
        shortBlob.GetHashCode().Should().Be(byteArray.GetHashCode());
        shortBlob.CompareTo(byteArray).Should().Be(0);
        // also equal to the equivalent multi-segment sequence (cross-kind)
        var sequence = Sequence(bytes);
        (shortBlob == sequence).Should().BeTrue();
        sequence.GetHashCode().Should().Be(shortBlob.GetHashCode());
        shortBlob.CompareTo(sequence).Should().Be(0);

        //Act
        // CopyTo round-trips
        Span<byte> copy = stackalloc byte[shortBlob.GetByteCount()];

        //Assert
        shortBlob.CopyTo(copy).Should().Be(bytes.Length);
        copy.SequenceEqual(bytes).Should().BeTrue();
    }

    [Fact]
    public void short_blob_numeric_content_equals_and_parses_like_integer()
    {
        var shortBlob = Short(Encoding.UTF8.GetBytes("1234"));
        shortBlob.Type.Should().Be(RedisValue.StorageType.ShortBlob);

        (shortBlob == 1234).Should().BeTrue();
        (1234 == shortBlob).Should().BeTrue();
        shortBlob.GetHashCode().Should().Be(((RedisValue)1234).GetHashCode()); // numeric-consistent hash

        shortBlob.TryParse(out long l).Should().BeTrue();
        l.Should().Be(1234);
        ((int)shortBlob).Should().Be(1234);
    }

    [Theory]
    [InlineData("inf", double.PositiveInfinity)]
    [InlineData("+inf", double.PositiveInfinity)]
    [InlineData("-inf", double.NegativeInfinity)]
    [InlineData("Inf", double.PositiveInfinity)] // case-insensitive
    [InlineData("nan", double.NaN)]
    public void short_blob_special_double_parses_like_byte_array(string text, double expected)
    {
        // regression: inf/nan are deliberately NOT folded by Simplify() (they'd break equality semantics),
        // so they stay blob-backed; a <= 8 byte payload is a ShortBlob. The (double) cast must parse it
        // exactly like the byte[] form. Previously the cast had no ShortBlob arm and threw for these.
        var bytes = Encoding.UTF8.GetBytes(text);
        var shortBlob = Short(bytes);
        RedisValue byteArray = (byte[])bytes.Clone();
        shortBlob.Type.Should().Be(RedisValue.StorageType.ShortBlob);
        byteArray.Type.Should().Be(RedisValue.StorageType.ByteArray);

        ((double)shortBlob).Should().Be(expected);
        ((double)shortBlob).Should().Be((double)byteArray); // indistinguishable from the byte[] form
    }

    [Fact]
    public void short_blob_starts_with_matches_byte_array()
    {
        var shortBlob = Short(Encoding.UTF8.GetBytes("abcde"));
        shortBlob.Type.Should().Be(RedisValue.StorageType.ShortBlob);

        shortBlob.StartsWith("abc"u8.ToArray()).Should().BeTrue();
        shortBlob.StartsWith(Short("ab"u8.ToArray())).Should().BeTrue(); // ShortBlob vs ShortBlob
        shortBlob.StartsWith(Sequence("abc"u8.ToArray())).Should().BeTrue(); // ShortBlob vs Sequence
        shortBlob.StartsWith("abd"u8.ToArray()).Should().BeFalse();
        shortBlob.StartsWith("abcdef"u8.ToArray()).Should().BeFalse();
    }

    [Fact]
    public void short_blob_compare_to_sequence_non_equal_ordering()
    {
        //Arrange
        // the equal-content tests only assert CompareTo == 0; this pins the *non-zero* cross-kind branches
        // of BlobCompareTo (contiguous ShortBlob vs multi-segment Sequence), in both directions.
        var abc = Short("abc"u8.ToArray());
        var abd = Sequence("abd"u8.ToArray());
        // differing content: "abc" < "abd"
        (abc.CompareTo(abd) < 0).Should().BeTrue();
        // ShortBlob (x) vs Sequence (y) - the ySeq branch
        (abd.CompareTo(abc) > 0).Should().BeTrue();
        // Sequence (x) vs ShortBlob (y) - the negated xSeq branch

        // length mismatch: "ab" is a prefix of "abc", so the shorter value sorts first
        var ab = Short("ab"u8.ToArray());

        //Act
        var abcSeq = Sequence("abc"u8.ToArray());

        //Assert
        (ab.CompareTo(abcSeq) < 0).Should().BeTrue();
        (abcSeq.CompareTo(ab) > 0).Should().BeTrue();
    }

    [Fact]
    public void from_raw_routes_by_length()
    {
        Short(Array.Empty<byte>()).Type.Should().Be(RedisValue.StorageType.String); // empty => EmptyString
        Short(new byte[8]).Type.Should().Be(RedisValue.StorageType.ShortBlob); // 8 => inline
        Short(new byte[9]).Type.Should().Be(RedisValue.StorageType.ByteArray); // 9 => allocate
    }

    [Fact]
    public void short_blob_non_text_bytes_round_trip_and_equal_byte_array()
    {
        // arbitrary non-UTF8 bytes including zero and high bytes, exactly 8 long (max inline)
        var bytes = new byte[] { 0x00, 0x01, 0xFF, 0x80, 0x7F, 0xAB, 0x00, 0xCD };
        var shortBlob = Short(bytes);
        RedisValue byteArray = (byte[])bytes.Clone();

        shortBlob.Type.Should().Be(RedisValue.StorageType.ShortBlob);
        (shortBlob == byteArray).Should().BeTrue();
        shortBlob.GetHashCode().Should().Be(byteArray.GetHashCode());
        ((byte[]?)shortBlob).Should().Equal(bytes);
        shortBlob.CompareTo(byteArray).Should().Be(0);
    }

    [Fact]
    public void short_blob_write_bulk_string_matches_byte_array()
    {
        var bytes = Encoding.UTF8.GetBytes("hello"); // 5 bytes => ShortBlob
        var shortBlob = Short(bytes);
        RedisValue byteArray = (byte[])bytes.Clone();
        shortBlob.Type.Should().Be(RedisValue.StorageType.ShortBlob);

        var fromShortBlob = WriteBulkString(shortBlob);
        var fromByteArray = WriteBulkString(byteArray);

        // the wire bytes must be identical, and a well-formed RESP bulk string
        fromShortBlob.Should().Equal(fromByteArray);
        fromShortBlob.AsSpan().SequenceEqual("$5\r\nhello\r\n"u8).Should().BeTrue("RESP bulk string mismatch");

        // serialize a single bulk string via the shared block buffer and copy out the exact wire bytes before
        // releasing it (we avoid ArrayBufferWriter<byte>, which isn't available on net48x)
        static byte[] WriteBulkString(RedisValue value)
        {
            ReadOnlyMemory<byte> payload = default;
            try
            {
                MessageWriter.WriteBulkString(value, MessageWriter.BlockBuffer);
                payload = MessageWriter.FlushBlockBuffer();
                return payload.ToArray();
            }
            catch
            {
                MessageWriter.RevertBlockBuffer();
                throw;
            }
            finally
            {
                MessageWriter.ReleaseBlockBuffer(payload);
            }
        }
    }

    [Fact]
    public void short_blob_not_null_or_empty()
    {
        var shortBlob = Short(new byte[] { 0 }); // a single zero byte
        shortBlob.Type.Should().Be(RedisValue.StorageType.ShortBlob);
        shortBlob.IsNull.Should().BeFalse();
        shortBlob.IsNullOrEmpty.Should().BeFalse();
        shortBlob.HasValue.Should().BeTrue();
    }
}
