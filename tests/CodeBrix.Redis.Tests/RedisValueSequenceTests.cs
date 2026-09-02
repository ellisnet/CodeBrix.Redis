using System;
using System.Buffers;
using System.Linq;
using System.Text;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

/// <summary>
/// Tests for <see cref="RedisValue"/> backed by a multi-segment <see cref="ReadOnlySequence{T}"/>
/// (<see cref="RedisValue.StorageType.Sequence"/>), focusing on text handling where a multi-byte UTF-8
/// glyph can straddle a segment boundary.
/// </summary>
public class RedisValueSequenceTests
{
    [Theory]
    [InlineData("")] // empty
    [InlineData("hello")] // ASCII only
    [InlineData("héllo")] // 2-byte glyph (é)
    [InlineData("€100")] // 3-byte glyph (€)
    [InlineData("a\U0001F389b")] // 4-byte glyph / surrogate pair (🎉)
    [InlineData("é€\U0001F389")] // adjacent multi-byte glyphs of differing widths
    [InlineData("héllo € wörld \U0001F389 mixed")] // mixed widths
    public void multi_segment_utf8_decodes_across_boundaries(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);

        // split at *every* byte so multi-byte glyphs are guaranteed to straddle segments
        RedisValue value = SplitEveryByte(bytes);

        // empty collapses to the empty string; anything else stays a genuine multi-segment sequence
        if (bytes.Length == 0)
        {
            value.Type.Should().Be(RedisValue.StorageType.String);
        }
        else
        {
            value.Type.Should().Be(RedisValue.StorageType.Sequence);
        }

        // byte length is unaffected by where we slice
        value.GetByteCount().Should().Be(bytes.Length);

        // the bug under test: a naive per-segment char count over-counts split glyphs; this must match
        // the contiguous count
        value.GetCharCount().Should().Be(text.Length);

        // GetMaxCharCount must remain a safe upper bound
        (value.GetMaxCharCount() >= text.Length).Should().BeTrue();

        // text round-trips via ToString / the string operator (Format.GetString linearizes first)
        value.ToString().Should().Be(text);
        ((string?)value).Should().Be(text);

        // ...and via the char-span copy (GetChars over the sequence), sized from GetCharCount
        var dest = new char[value.GetCharCount()];
        int written = value.CopyTo(dest.AsSpan());
        written.Should().Be(text.Length);
        (new string(dest, 0, written)).Should().Be(text);
    }

    [Fact]
    public void large_multi_segment_utf8_uses_streaming_decoder_path()
    {
        //Arrange
        // exceed the helper's stack-linearize threshold so the streaming Decoder path is exercised, with
        // every byte in its own segment so multi-byte glyphs straddle boundaries throughout
        var text = string.Concat(Enumerable.Repeat("héllo-€-\U0001F389-", 20));
        var bytes = Encoding.UTF8.GetBytes(text);
        (bytes.Length > 128).Should().BeTrue($"expected a payload over the linearize threshold, got {bytes.Length}");
        RedisValue value = SplitEveryByte(bytes);
        value.Type.Should().Be(RedisValue.StorageType.Sequence);
        value.GetCharCount().Should().Be(text.Length);
        value.ToString().Should().Be(text);
        var dest = new char[value.GetCharCount()];

        //Act
        int written = value.CopyTo(dest.AsSpan());

        //Assert
        written.Should().Be(text.Length);
        (new string(dest, 0, written)).Should().Be(text);
    }

    [Theory]
    [InlineData("10")] // numeric: simplifies to an integer, so all forms hash as Int64
    [InlineData("10.5")] // numeric: simplifies to a double
    [InlineData("hello")] // plain text: compared/hashed as a string
    [InlineData("inf")] // special-case text that deliberately does NOT simplify to a double
    [InlineData("nan")]
    public void equal_values_hash_identically_across_all_storage_forms(string text)
    {
        //Arrange
        var bytes = Encoding.UTF8.GetBytes(text);
        RedisValue asString = text;
        RedisValue asBytes = bytes;
        // single-buffer (ByteArray)
        RedisValue asSequence = SplitEveryByte(bytes);
        // multi-buffer (Sequence)

        asString.Type.Should().Be(RedisValue.StorageType.String);
        asBytes.Type.Should().Be(RedisValue.StorageType.ByteArray);
        asSequence.Type.Should().Be(RedisValue.StorageType.Sequence);
        // all forms are equal to one another...
        (asString == asBytes).Should().BeTrue("string == bytes");
        (asString == asSequence).Should().BeTrue("string == sequence");
        (asBytes == asSequence).Should().BeTrue("bytes == sequence");

        //Act
        // ...so the equality/hash contract demands identical hash codes
        int expected = asString.GetHashCode();

        //Assert
        asBytes.GetHashCode().Should().Be(expected);
        asSequence.GetHashCode().Should().Be(expected);
    }

    [Fact]
    public void integer_and_text_forms_hash_identically()
    {
        // the canonical example: 10, "10", and its bytes (single- and multi-buffer) all hash the same
        RedisValue asInt = 10;
        RedisValue asString = "10";
        RedisValue asBytes = new byte[] { (byte)'1', (byte)'0' };
        RedisValue asSequence = SplitEveryByte(new byte[] { (byte)'1', (byte)'0' });

        asSequence.Type.Should().Be(RedisValue.StorageType.Sequence);

        int expected = asInt.GetHashCode();
        asString.GetHashCode().Should().Be(expected);
        asBytes.GetHashCode().Should().Be(expected);
        asSequence.GetHashCode().Should().Be(expected);
    }

    [Theory]
    [InlineData("123")] // integer
    [InlineData("-123")] // negative integer
    [InlineData("00")] // leading zeros, within length limit
    [InlineData("123.5")] // non-integer double
    [InlineData("-0.25")] // negative double
    [InlineData("abc")] // not numeric at all
    [InlineData("12x")] // partially numeric (must not parse)
    [InlineData("99999999999999999999999")] // oversize: cannot be Int64 or double-as-int
    public void multi_segment_sequence_try_parse_matches_byte_array(string text)
    {
        //Arrange
        var bytes = Encoding.UTF8.GetBytes(text);
        RedisValue asBytes = bytes;

        //Act
        // single-buffer (ByteArray)
        RedisValue asSequence = SplitEveryByte(bytes);

        //Assert
        // multi-buffer (Sequence)
        asSequence.Type.Should().Be(RedisValue.StorageType.Sequence);
        // a sequence-backed value must parse exactly like the equivalent byte[]
        asSequence.TryParse(out long actualLong).Should().Be(asBytes.TryParse(out long expectedLong));
        actualLong.Should().Be(expectedLong);
        asSequence.TryParse(out int actualInt).Should().Be(asBytes.TryParse(out int expectedInt));
        actualInt.Should().Be(expectedInt);
        asSequence.TryParse(out double actualDouble).Should().Be(asBytes.TryParse(out double expectedDouble));
        actualDouble.Should().Be(expectedDouble);
    }

    [Theory]
    [InlineData("abc", "abc")] // equal
    [InlineData("abc", "abd")] // differ at last byte
    [InlineData("abd", "abc")]
    [InlineData("xbc", "abc")] // differ at first byte
    [InlineData("abc", "abcd")] // prefix: shorter sorts first
    [InlineData("abcd", "abc")]
    [InlineData("abcdefardvark", "abcdefardwolf")] // longer, differ mid-way
    public void multi_segment_sequence_compare_to_matches_byte_ordinal(string x, string y)
    {
        var bx = Encoding.UTF8.GetBytes(x);
        var by = Encoding.UTF8.GetBytes(y);
        int expected = Math.Sign(((ReadOnlySpan<byte>)bx).SequenceCompareTo(by));

        RedisValue seqX = SplitEveryByte(bx), seqY = SplitEveryByte(by);
        RedisValue arrX = bx, arrY = by;
        seqX.Type.Should().Be(RedisValue.StorageType.Sequence);
        seqY.Type.Should().Be(RedisValue.StorageType.Sequence);

        Math.Sign(seqX.CompareTo(seqY)).Should().Be(expected); // sequence vs sequence
        Math.Sign(seqX.CompareTo(arrY)).Should().Be(expected); // sequence vs byte[]
        Math.Sign(arrX.CompareTo(seqY)).Should().Be(expected); // byte[] vs sequence
        Math.Sign(arrX.CompareTo(arrY)).Should().Be(expected); // byte[] vs byte[] baseline
    }

    [Fact]
    public void multi_segment_sequence_compare_to_equal_content_different_boundaries()
    {
        //Arrange
        // identical content, but segmented differently on each side - the tandem walk must still see equality
        var bytes = Encoding.UTF8.GetBytes("the quick brown fox");
        RedisValue a = FragmentedSegment<byte>.Create(Mem(bytes, 0, 4), Mem(bytes, 4, 7), Mem(bytes, 11, bytes.Length - 11));

        //Act
        RedisValue b = FragmentedSegment<byte>.Create(Mem(bytes, 0, 2), Mem(bytes, 2, 7), Mem(bytes, 9, 6), Mem(bytes, 15, bytes.Length - 15));

        //Assert
        a.Type.Should().Be(RedisValue.StorageType.Sequence);
        b.Type.Should().Be(RedisValue.StorageType.Sequence);
        a.CompareTo(b).Should().Be(0);
        b.CompareTo(a).Should().Be(0);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void multi_segment_sequence_compare_to_difference_across_boundaries()
    {
        // the differing byte (index 5: 'f' vs 'X') sits in a different segment on each side
        var x = Encoding.UTF8.GetBytes("abcdefgh");
        var y = Encoding.UTF8.GetBytes("abcdeXgh");
        RedisValue sx = FragmentedSegment<byte>.Create(Mem(x, 0, 3), Mem(x, 3, x.Length - 3)); // [abc][defgh]
        RedisValue sy = FragmentedSegment<byte>.Create(Mem(y, 0, 6), Mem(y, 6, y.Length - 6)); // [abcdeX][gh]

        int expected = Math.Sign(((ReadOnlySpan<byte>)x).SequenceCompareTo(y)); // 'f' > 'X' => positive
        Math.Sign(sx.CompareTo(sy)).Should().Be(expected);
        Math.Sign(sy.CompareTo(sx)).Should().Be(-expected); // antisymmetry
    }

    [Theory]
    [InlineData("123")] // integer-valued
    [InlineData("-123")]
    [InlineData("123.5")] // fractional
    [InlineData("inf")] // special doubles: deliberately not simplified, so they exercise the cast's text fallback
    [InlineData("+inf")]
    [InlineData("-inf")]
    [InlineData("nan")]
    public void multi_segment_sequence_double_cast_matches_byte_array(string text)
    {
        //Arrange
        var bytes = Encoding.UTF8.GetBytes(text);
        RedisValue asBytes = bytes;

        //Act
        // single-buffer (ByteArray)
        RedisValue asSequence = SplitEveryByte(bytes);

        //Assert
        // multi-buffer (Sequence)
        asSequence.Type.Should().Be(RedisValue.StorageType.Sequence);
        // the (double) cast must behave the same for a sequence as for the equivalent byte[]
        ((double)asSequence).Should().Be((double)asBytes);
    }

    [Fact]
    public void multi_segment_bytes_round_trip_to_array()
    {
        //Arrange
        var bytes = Encoding.UTF8.GetBytes("the quick brown fox");

        //Act
        RedisValue value = SplitEveryByte(bytes);

        //Assert
        value.Type.Should().Be(RedisValue.StorageType.Sequence);
        ((byte[]?)value).Should().Equal(bytes);
    }

    private static RedisValue SplitEveryByte(byte[] bytes)
    {
        var chunks = new ReadOnlyMemory<byte>[bytes.Length];
        for (int i = 0; i < bytes.Length; i++)
        {
            chunks[i] = new ReadOnlyMemory<byte>(bytes, i, 1);
        }
        return FragmentedSegment<byte>.Create(chunks);
    }

    // a slice of the source as ReadOnlyMemory (no array allocation, and avoids range syntax for net481)
    private static ReadOnlyMemory<byte> Mem(byte[] source, int start, int length) => new(source, start, length);
}
