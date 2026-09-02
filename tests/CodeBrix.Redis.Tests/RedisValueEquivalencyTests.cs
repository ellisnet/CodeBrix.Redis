using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class RedisValueEquivalencyUnitTests
{
    // internal storage types: null, integer, double, string, raw
    // public perceived types: int, long, double, bool, memory / byte[]
    [Fact]
    public void int32_matrix()
    {
        static void Check(RedisValue known, RedisValue test)
        {
            KeyAndValueTests.CheckSame(known, test);
            if (known.IsNull)
            {
                test.IsNull.Should().BeTrue();
                (((int?)test).HasValue).Should().BeFalse();
            }
            else
            {
                test.IsNull.Should().BeFalse();
                (((int?)test)!.Value).Should().Be((int)known);
                ((int)test).Should().Be((int)known);
            }
            ((int)test).Should().Be((int)known);
        }
        Check(42, 42);
        Check(42, 42.0);
        Check(42, "42");
        Check(42, "42.0");
        Check(42, Bytes("42"u8));
        Check(42, Bytes("42.0"u8));
        Check(42, Bytes("4"u8, "2"u8)); // multi-segment sequence
        Check(42, Bytes("4"u8, "2.0"u8)); // multi-segment sequence
        CheckString(42, "42");

        Check(-42, -42);
        Check(-42, -42.0);
        Check(-42, "-42");
        Check(-42, "-42.0");
        Check(-42, Bytes("-42"u8));
        Check(-42, Bytes("-42.0"u8));
        Check(-42, Bytes("-"u8, "42"u8)); // multi-segment sequence
        Check(-42, Bytes("-4"u8, "2"u8, ".0"u8)); // multi-segment sequence (3 segments)
        CheckString(-42, "-42");

        Check(1, true);
        Check(0, false);
    }

    [Fact]
    public void int64_matrix()
    {
        static void Check(RedisValue known, RedisValue test)
        {
            KeyAndValueTests.CheckSame(known, test);
            if (known.IsNull)
            {
                test.IsNull.Should().BeTrue();
                (((long?)test).HasValue).Should().BeFalse();
            }
            else
            {
                test.IsNull.Should().BeFalse();
                (((long?)test!).Value).Should().Be((long)known);
                ((long)test).Should().Be((long)known);
            }
            ((long)test).Should().Be((long)known);
        }
        Check(1099511627848, 1099511627848);
        Check(1099511627848, 1099511627848.0);
        Check(1099511627848, "1099511627848");
        Check(1099511627848, "1099511627848.0");
        Check(1099511627848, Bytes("1099511627848"u8));
        Check(1099511627848, Bytes("1099511627848.0"u8));
        Check(1099511627848, Bytes("109951"u8, "1627848"u8)); // multi-segment sequence
        Check(1099511627848, Bytes("109951"u8, "1627848"u8, ".0"u8)); // multi-segment sequence
        CheckString(1099511627848, "1099511627848");

        Check(-1099511627848, -1099511627848);
        Check(-1099511627848, -1099511627848);
        Check(-1099511627848, "-1099511627848");
        Check(-1099511627848, "-1099511627848.0");
        Check(-1099511627848, Bytes("-1099511627848"u8));
        Check(-1099511627848, Bytes("-1099511627848.0"u8));
        Check(-1099511627848, Bytes("-109951"u8, "1627848"u8)); // multi-segment sequence
        CheckString(-1099511627848, "-1099511627848");

        Check(1L, true);
        Check(0L, false);
    }

    [Fact]
    public void double_matrix()
    {
        static void Check(RedisValue known, RedisValue test)
        {
            KeyAndValueTests.CheckSame(known, test);
            if (known.IsNull)
            {
                test.IsNull.Should().BeTrue();
                (((double?)test).HasValue).Should().BeFalse();
            }
            else
            {
                test.IsNull.Should().BeFalse();
                (((double?)test)!.Value).Should().Be((double)known);
                ((double)test).Should().Be((double)known);
            }
            ((double)test).Should().Be((double)known);
        }
        Check(1099511627848.0, 1099511627848);
        Check(1099511627848.0, 1099511627848.0);
        Check(1099511627848.0, "1099511627848");
        Check(1099511627848.0, "1099511627848.0");
        Check(1099511627848.0, Bytes("1099511627848"u8));
        Check(1099511627848.0, Bytes("1099511627848.0"u8));
        Check(1099511627848.0, Bytes("109951"u8, "1627848"u8)); // multi-segment sequence
        Check(1099511627848.0, Bytes("1099511627848"u8, ".0"u8)); // multi-segment sequence
        CheckString(1099511627848.0, "1099511627848");

        Check(-1099511627848.0, -1099511627848);
        Check(-1099511627848.0, -1099511627848);
        Check(-1099511627848.0, "-1099511627848");
        Check(-1099511627848.0, "-1099511627848.0");
        Check(-1099511627848.0, Bytes("-1099511627848"u8));
        Check(-1099511627848.0, Bytes("-1099511627848.0"u8));
        CheckString(-1099511627848.0, "-1099511627848");

        Check(1.0, true);
        Check(0.0, false);

        Check(1099511627848.6001, 1099511627848.6001);
        Check(1099511627848.6001, "1099511627848.6001");
        Check(1099511627848.6001, Bytes("1099511627848.6001"u8));
        Check(1099511627848.6001, Bytes("1099511627848"u8, ".6001"u8)); // multi-segment sequence
        CheckString(1099511627848.6001, "1099511627848.6001");

        Check(-1099511627848.6001, -1099511627848.6001);
        Check(-1099511627848.6001, "-1099511627848.6001");
        Check(-1099511627848.6001, Bytes("-1099511627848.6001"u8));
        CheckString(-1099511627848.6001, "-1099511627848.6001");

        Check(double.NegativeInfinity, double.NegativeInfinity);
        CheckString(double.NegativeInfinity, "-inf");

        Check(double.PositiveInfinity, double.PositiveInfinity);
        CheckString(double.PositiveInfinity, "+inf");

        Check(double.NaN, double.NaN);
        CheckString(double.NaN, "NaN");
    }

    [Theory]
    [InlineData("na")]
    [InlineData("nan")]
    [InlineData("nans")]
    [InlineData("in")]
    [InlineData("inf")]
    [InlineData("info")]
    public void special_case_equality_rules_string(string value)
    {
        RedisValue x = value, y = value;
        y.Should().Be(x);

        x.Equals(y).Should().BeTrue();
        y.Equals(x).Should().BeTrue();
        (x == y).Should().BeTrue();
        (y == x).Should().BeTrue();
        (x != y).Should().BeFalse();
        (y != x).Should().BeFalse();
        y.GetHashCode().Should().Be(x.GetHashCode());
    }

    [Theory]
    [InlineData("na")]
    [InlineData("nan")]
    [InlineData("nans")]
    [InlineData("in")]
    [InlineData("inf")]
    [InlineData("info")]
    public void special_case_equality_rules_bytes(string value)
    {
        //Arrange
        byte[] bytes0 = Encoding.UTF8.GetBytes(value),
               bytes1 = Encoding.UTF8.GetBytes(value);
        bytes1.Should().NotBeSameAs(bytes0);

        //Act
        RedisValue x = bytes0, y = bytes1;

        //Assert
        x.Equals(y).Should().BeTrue();
        y.Equals(x).Should().BeTrue();
        (x == y).Should().BeTrue();
        (y == x).Should().BeTrue();
        (x != y).Should().BeFalse();
        (y != x).Should().BeFalse();
        y.GetHashCode().Should().Be(x.GetHashCode());
    }

    [Theory]
    [InlineData("na")]
    [InlineData("nan")]
    [InlineData("nans")]
    [InlineData("in")]
    [InlineData("inf")]
    [InlineData("info")]
    public void special_case_equality_rules_hybrid(string value)
    {
        //Arrange
        byte[] bytes = Encoding.UTF8.GetBytes(value);

        //Act
        RedisValue x = bytes, y = value;

        //Assert
        x.Equals(y).Should().BeTrue();
        y.Equals(x).Should().BeTrue();
        (x == y).Should().BeTrue();
        (y == x).Should().BeTrue();
        (x != y).Should().BeFalse();
        (y != x).Should().BeFalse();
        y.GetHashCode().Should().Be(x.GetHashCode());
    }

    [Theory]
    [InlineData("na", "NA")]
    [InlineData("nan", "NAN")]
    [InlineData("nans", "NANS")]
    [InlineData("in", "IN")]
    [InlineData("inf", "INF")]
    [InlineData("info", "INFO")]
    public void special_case_non_equality_rules_string(string s, string t)
    {
        RedisValue x = s, y = t;
        x.Equals(y).Should().BeFalse();
        y.Equals(x).Should().BeFalse();
        (x == y).Should().BeFalse();
        (y == x).Should().BeFalse();
        (x != y).Should().BeTrue();
        (y != x).Should().BeTrue();
    }

    [Theory]
    [InlineData("na", "NA")]
    [InlineData("nan", "NAN")]
    [InlineData("nans", "NANS")]
    [InlineData("in", "IN")]
    [InlineData("inf", "INF")]
    [InlineData("info", "INFO")]
    public void special_case_non_equality_rules_bytes(string s, string t)
    {
        RedisValue x = Encoding.UTF8.GetBytes(s), y = Encoding.UTF8.GetBytes(t);
        x.Equals(y).Should().BeFalse();
        y.Equals(x).Should().BeFalse();
        (x == y).Should().BeFalse();
        (y == x).Should().BeFalse();
        (x != y).Should().BeTrue();
        (y != x).Should().BeTrue();
    }

    [Theory]
    [InlineData("na", "NA")]
    [InlineData("nan", "NAN")]
    [InlineData("nans", "NANS")]
    [InlineData("in", "IN")]
    [InlineData("inf", "INF")]
    [InlineData("info", "INFO")]
    public void special_case_non_equality_rules_hybrid(string s, string t)
    {
        RedisValue x = s, y = Encoding.UTF8.GetBytes(t);
        x.Equals(y).Should().BeFalse();
        y.Equals(x).Should().BeFalse();
        (x == y).Should().BeFalse();
        (y == x).Should().BeFalse();
        (x != y).Should().BeTrue();
        (y != x).Should().BeTrue();
    }

    private static void CheckString(RedisValue value, string expected)
    {
        var s = value.ToString();
        (s == expected).Should().BeTrue($"'{s}' vs '{expected}'");
    }

    // single contiguous buffer => stored as a byte[] (StorageType.ByteArray)
    private static RedisValue Bytes(ReadOnlySpan<byte> value) => value.ToArray();

    // multiple chunks => a (deliberately) multi-segment ReadOnlySequence<byte> (StorageType.Sequence).
    // We trust the single-segment collapse logic, so callers pass >= 2 chunks to exercise the sequence path.
    private static RedisValue Bytes(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        => FragmentedSegment<byte>.Create(a.ToArray(), b.ToArray());

    private static RedisValue Bytes(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, ReadOnlySpan<byte> c)
        => FragmentedSegment<byte>.Create(a.ToArray(), b.ToArray(), c.ToArray());

    private static string LineNumber([CallerLineNumber] int lineNumber = 0) => lineNumber.ToString();

    [Fact]
    public void redis_value_starts_with()
    {
        //Arrange
        // test strings
        RedisValue x = "abc";
        x.StartsWith("a").Should().BeTrue(LineNumber());
        x.StartsWith("ab").Should().BeTrue(LineNumber());
        x.StartsWith("abc").Should().BeTrue(LineNumber());
        x.StartsWith("abd").Should().BeFalse(LineNumber());
        x.StartsWith("abcd").Should().BeFalse(LineNumber());
        x.StartsWith(123).Should().BeFalse(LineNumber());
        x.StartsWith(false).Should().BeFalse(LineNumber());
        // test binary
        x = Encoding.ASCII.GetBytes("abc");
        x.StartsWith("a").Should().BeTrue(LineNumber());
        x.StartsWith("ab").Should().BeTrue(LineNumber());
        x.StartsWith("abc").Should().BeTrue(LineNumber());
        x.StartsWith("abd").Should().BeFalse(LineNumber());
        x.StartsWith("abcd").Should().BeFalse(LineNumber());
        x.StartsWith(123).Should().BeFalse(LineNumber());
        x.StartsWith(false).Should().BeFalse(LineNumber());
        x.StartsWith((RedisValue)Encoding.ASCII.GetBytes("a")).Should().BeTrue(LineNumber());
        x.StartsWith((RedisValue)Encoding.ASCII.GetBytes("ab")).Should().BeTrue(LineNumber());
        x.StartsWith((RedisValue)Encoding.ASCII.GetBytes("abc")).Should().BeTrue(LineNumber());
        x.StartsWith((RedisValue)Encoding.ASCII.GetBytes("abd")).Should().BeFalse(LineNumber());
        x.StartsWith((RedisValue)Encoding.ASCII.GetBytes("abcd")).Should().BeFalse(LineNumber());
        x.StartsWith("a"u8).Should().BeTrue(LineNumber());
        x.StartsWith("ab"u8).Should().BeTrue(LineNumber());
        x.StartsWith("abc"u8).Should().BeTrue(LineNumber());
        x.StartsWith("abd"u8).Should().BeFalse(LineNumber());
        x.StartsWith("abcd"u8).Should().BeFalse(LineNumber());

        //Act
        x = 10;

        //Assert
        // integers are effectively strings in this context
        x.StartsWith(1).Should().BeTrue(LineNumber());
        x.StartsWith(10).Should().BeTrue(LineNumber());
        x.StartsWith(100).Should().BeFalse(LineNumber());
    }

    private static ReadOnlySpan<byte> Raw(params byte[] value) => value;

    // The third member of the contiguous-blob storage arm. Unlike a short blob or a byte[], no ordinary
    // conversion produces one, so fabricate it the way the toy server does - otherwise the kind goes untested
    // purely because it is awkward to reach.
    private sealed class ByteMemoryManager(byte[] value) : MemoryManager<byte>
    {
        public override Span<byte> GetSpan() => value;
        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
        public override void Unpin() => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { }
    }

    private static RedisValue Managed(ReadOnlySpan<byte> value)
    {
        var arr = value.ToArray();
        return RedisValue.CreateForeign(new ByteMemoryManager(arr), 0, arr.Length);
    }

    /// <summary>
    /// <see cref="RedisValue.EndsWithAscii"/> takes a different route for every storage kind - that is the
    /// whole point of it - so every kind gets asserted here, and each is checked against the kind it actually
    /// landed in. A test that only exercised strings would prove close to nothing.
    /// </summary>
    [Fact]
    public void redis_value_ends_with_ascii()
    {
        const byte Star = (byte)'*', Zero = (byte)'0', Five = (byte)'5', Eight = (byte)'8';

        // null and empty have no text, so nothing to end with
        Check(RedisValue.Null, RedisValue.StorageType.Null, Star, false);
        Check(RedisValue.EmptyString, RedisValue.StorageType.String, Star, false);

        // string
        Check("*", RedisValue.StorageType.String, Star, true);
        Check("5-*", RedisValue.StorageType.String, Star, true);
        Check("5-5", RedisValue.StorageType.String, Star, false);
        Check("5-5", RedisValue.StorageType.String, Five, true);
        Check("*x", RedisValue.StorageType.String, Star, false);

        // a non-ASCII tail can never match an ASCII byte, and must not be mistaken for one: the last UTF-8
        // byte of "é" is A9, and (char)0xA9 is a perfectly real char - so comparing chars is what keeps this
        // right, where comparing the encoded tail byte-for-byte would need the encode we are avoiding
        Check("é", RedisValue.StorageType.String, 0x29, false); // 0xA9 & 0x7F, had we masked
        Check("é*", RedisValue.StorageType.String, Star, true);

        // short blob (<= 8 bytes, held inline) and byte array (longer), which share a code path but not a kind
        Check(RedisValue.FromRaw("5-*"u8), RedisValue.StorageType.ShortBlob, Star, true);
        Check(RedisValue.FromRaw("5-5"u8), RedisValue.StorageType.ShortBlob, Star, false);
        Check(Bytes("1526919030474-*"u8), RedisValue.StorageType.ByteArray, Star, true);
        Check(Bytes("1526919030474-55"u8), RedisValue.StorageType.ByteArray, Star, false);
        Check(Managed("5-*"u8), RedisValue.StorageType.MemoryManager, Star, true);
        Check(Managed("5-5"u8), RedisValue.StorageType.MemoryManager, Star, false);

        // multi-segment sequence: the last byte is in the final segment, and also the case where that
        // segment holds only it
        Check(Bytes("5-"u8, "*"u8), RedisValue.StorageType.Sequence, Star, true);
        Check(Bytes("5"u8, "-*"u8), RedisValue.StorageType.Sequence, Star, true);
        Check(Bytes("5"u8, "-*"u8, "x"u8), RedisValue.StorageType.Sequence, Star, false);

        // integers: the final digit, without formatting anything
        Check(5, RedisValue.StorageType.Int64, Five, true);
        Check(5, RedisValue.StorageType.Int64, Star, false);
        Check(0, RedisValue.StorageType.Int64, Zero, true);
        Check(15, RedisValue.StorageType.Int64, Five, true);
        Check(15, RedisValue.StorageType.Int64, (byte)'1', false);

        // negatives take the sign off the *remainder*, not the value - so the extreme is the interesting one
        Check(-5, RedisValue.StorageType.Int64, Five, true);
        Check(-15, RedisValue.StorageType.Int64, Five, true);
        Check(long.MinValue, RedisValue.StorageType.Int64, Eight, true); // -9223372036854775808
        Check(long.MaxValue, RedisValue.StorageType.Int64, (byte)'7', true); // 9223372036854775807
        Check(ulong.MaxValue, RedisValue.StorageType.UInt64, Five, true); // 18446744073709551615

        // doubles format, which is fine; "inf" is the case worth pinning, since it is text rather than digits
        Check(1.5, RedisValue.StorageType.Double, Five, true);
        Check(1.5, RedisValue.StorageType.Double, Star, false);
        Check(double.PositiveInfinity, RedisValue.StorageType.Double, (byte)'f', true);
        Check(double.NegativeInfinity, RedisValue.StorageType.Double, (byte)'f', true);

        static void Check(RedisValue value, RedisValue.StorageType expectedKind, byte test, bool expected)
        {
            value.Type.Should().Be(expectedKind);
            value.EndsWithAscii(test).Should().Be(expected);
        }
    }

    [Fact]
    // The answer cannot depend on how the same value happens to be stored, which is the property the whole
    // per-kind switch has to preserve - so run one value through every kind that can hold it.
    public void redis_value_ends_with_ascii_agrees_across_storage_kinds()
    {
        RedisValue[] fifteen =
        {
            15,
            15u,
            15.0,
            "15",
            RedisValue.FromRaw("15"u8),
            Bytes("15"u8),
            Bytes("1"u8, "5"u8),
            Managed("15"u8),
        };

        foreach (var value in fifteen)
        {
            value.EndsWithAscii((byte)'5').Should().BeTrue(value.Type.ToString());
            value.EndsWithAscii((byte)'1').Should().BeFalse(value.Type.ToString());
            value.EndsWithAscii((byte)'*').Should().BeFalse(value.Type.ToString());
        }
    }

    [Fact]
    // A string-backed value holds UTF-16, and the prefix is bytes; the two do not have the same length, so
    // deciding "too short to match" by comparing char count against byte count is wrong the moment anything
    // is not ASCII. "e-acute, euro" is 2 chars but 5 UTF-8 bytes, so every prefix of 3 bytes or more was
    // rejected out of hand.
    public void redis_value_starts_with_multi_byte_utf8_string()
    {
        //Arrange
        RedisValue x = "é€";
        // C3 A9 E2 82 AC
        x.Length().Should().Be(5);
        x.StartsWith(Raw(0xC3)).Should().BeTrue(LineNumber());
        x.StartsWith(Raw(0xC3, 0xA9)).Should().BeTrue(LineNumber());
        x.StartsWith(Raw(0xC3, 0xA9, 0xE2)).Should().BeTrue(LineNumber());
        x.StartsWith(Raw(0xC3, 0xA9, 0xE2, 0x82)).Should().BeTrue(LineNumber());
        x.StartsWith(Raw(0xC3, 0xA9, 0xE2, 0x82, 0xAC)).Should().BeTrue(LineNumber());
        x.StartsWith(Raw(0xC3, 0xA9, 0xE2, 0x82, 0xAD)).Should().BeFalse(LineNumber());
        x.StartsWith(Raw(0xC3, 0xA9, 0xE2, 0x82, 0xAC, 0x00)).Should().BeFalse(LineNumber());
        x.StartsWith(Raw(0xE2)).Should().BeFalse(LineNumber());

        //Act
        // the byte-backed spelling of the same value must of course agree
        RedisValue y = Encoding.UTF8.GetBytes("é€");

        //Assert
        y.StartsWith(Raw(0xC3, 0xA9, 0xE2)).Should().BeTrue(LineNumber());
    }

    [Fact]
    // The other half of the same problem: a prefix of N bytes needs at most N chars, so the string is cut to
    // that many before encoding - but cutting between the halves of a surrogate pair leaves a lone surrogate,
    // which the encoder replaces with U+FFFD (EF BF BD) rather than the bytes the caller is asking about.
    public void redis_value_starts_with_surrogate_pair()
    {
        RedisValue x = "a\U0001F600"; // 'a' + grinning face: 61 F0 9F 98 80
        x.Length().Should().Be(5);

        x.StartsWith(Raw(0x61)).Should().BeTrue(LineNumber());
        x.StartsWith(Raw(0x61, 0xF0)).Should().BeTrue(LineNumber());
        x.StartsWith(Raw(0x61, 0xF0, 0x9F)).Should().BeTrue(LineNumber());
        x.StartsWith(Raw(0x61, 0xF0, 0x9F, 0x98, 0x80)).Should().BeTrue(LineNumber());

        x.StartsWith(Raw(0x61, 0xEF)).Should().BeFalse(LineNumber()); // the U+FFFD a naive cut would produce
        x.StartsWith(Raw(0x61, 0xF0, 0x9F, 0x98, 0x81)).Should().BeFalse(LineNumber());
    }

    [Fact]
    public void try_parse_int64()
    {
        (((RedisValue)123).TryParse(out long l)).Should().BeTrue();
        l.Should().Be(123);

        (((RedisValue)123.0).TryParse(out l)).Should().BeTrue();
        l.Should().Be(123);

        (((RedisValue)(int.MaxValue + 123L)).TryParse(out l)).Should().BeTrue();
        l.Should().Be(int.MaxValue + 123L);

        (((RedisValue)"123").TryParse(out l)).Should().BeTrue();
        l.Should().Be(123);

        (((RedisValue)(-123)).TryParse(out l)).Should().BeTrue();
        l.Should().Be(-123);

        default(RedisValue).TryParse(out l).Should().BeTrue();
        l.Should().Be(0);

        (((RedisValue)123.0).TryParse(out l)).Should().BeTrue();
        l.Should().Be(123);

        (((RedisValue)"abc").TryParse(out long _)).Should().BeFalse();
        (((RedisValue)"123.1").TryParse(out long _)).Should().BeFalse();
        (((RedisValue)123.1).TryParse(out long _)).Should().BeFalse();
    }

    [Fact]
    public void try_parse_int32()
    {
        (((RedisValue)123).TryParse(out int i)).Should().BeTrue();
        i.Should().Be(123);

        (((RedisValue)123.0).TryParse(out i)).Should().BeTrue();
        i.Should().Be(123);

        (((RedisValue)(int.MaxValue + 123L)).TryParse(out int _)).Should().BeFalse();

        (((RedisValue)"123").TryParse(out i)).Should().BeTrue();
        i.Should().Be(123);

        (((RedisValue)(-123)).TryParse(out i)).Should().BeTrue();
        i.Should().Be(-123);

        default(RedisValue).TryParse(out i).Should().BeTrue();
        i.Should().Be(0);

        (((RedisValue)123.0).TryParse(out i)).Should().BeTrue();
        i.Should().Be(123);

        (((RedisValue)"abc").TryParse(out int _)).Should().BeFalse();
        (((RedisValue)"123.1").TryParse(out int _)).Should().BeFalse();
        (((RedisValue)123.1).TryParse(out int _)).Should().BeFalse();
    }

    [Fact]
    public void try_parse_double()
    {
        (((RedisValue)123).TryParse(out double d)).Should().BeTrue();
        d.Should().Be(123);

        (((RedisValue)123.0).TryParse(out d)).Should().BeTrue();
        d.Should().Be(123.0);

        (((RedisValue)123.1).TryParse(out d)).Should().BeTrue();
        d.Should().Be(123.1);

        (((RedisValue)(int.MaxValue + 123L)).TryParse(out d)).Should().BeTrue();
        d.Should().Be(int.MaxValue + 123L);

        (((RedisValue)"123").TryParse(out d)).Should().BeTrue();
        d.Should().Be(123.0);

        (((RedisValue)(-123)).TryParse(out d)).Should().BeTrue();
        d.Should().Be(-123.0);

        default(RedisValue).TryParse(out d).Should().BeTrue();
        d.Should().Be(0.0);

        (((RedisValue)123.0).TryParse(out d)).Should().BeTrue();
        d.Should().Be(123.0);

        (((RedisValue)"123.1").TryParse(out d)).Should().BeTrue();
        d.Should().Be(123.1);

        (((RedisValue)"abc").TryParse(out double _)).Should().BeFalse();
    }

    [Fact]
    public void redis_value_length_string()
    {
        RedisValue value = "abc";
        value.Type.Should().Be(RedisValue.StorageType.String);
        value.Length().Should().Be(3);
    }

    [Fact]
    public void redis_value_length_double()
    {
        RedisValue value = Math.PI;
        value.Type.Should().Be(RedisValue.StorageType.Double);
        value.Length().Should().Be(18);
    }

    [Fact]
    public void redis_value_length_int64()
    {
        RedisValue value = 123;
        value.Type.Should().Be(RedisValue.StorageType.Int64);
        value.Length().Should().Be(3);
    }

    [Fact]
    public void redis_value_length_u_int64()
    {
        RedisValue value = ulong.MaxValue - 5;
        value.Type.Should().Be(RedisValue.StorageType.UInt64);
        value.Length().Should().Be(20);
    }

    [Fact]
    public void redis_value_length_raw()
    {
        RedisValue value = new byte[] { 0, 1, 2 };
        value.Type.Should().Be(RedisValue.StorageType.ByteArray);
        value.Length().Should().Be(3);
    }

    [Fact]
    public void redis_value_length_null()
    {
        RedisValue value = RedisValue.Null;
        value.Type.Should().Be(RedisValue.StorageType.Null);
        value.Length().Should().Be(0);
    }
}
