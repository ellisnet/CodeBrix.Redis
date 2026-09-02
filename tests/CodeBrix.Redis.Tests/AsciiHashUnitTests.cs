using System;
using System.Runtime.InteropServices;
using System.Text;
using CodeBrix.Redis.Respite;
using SilverAssertions;
using Xunit;
using Xunit.Sdk;

// ReSharper disable InconsistentNaming - to better represent expected literals
// ReSharper disable IdentifierTypo
namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public partial class AsciiHashUnitTests
{
    // note: if the hashing algorithm changes, we can update the last parameter freely; it doesn't matter
    // what it *is* - what matters is that we can see that it has entropy between different values
    [Theory]
    [InlineData(1, A.Length, A.Text, A.HashCS, 97)]
    [InlineData(2, AB.Length, AB.Text, AB.HashCS, 25185)]
    [InlineData(3, ABC.Length, ABC.Text, ABC.HashCS, 6513249)]
    [InlineData(4, ABCD.Length, ABCD.Text, ABCD.HashCS, 1684234849)]
    [InlineData(5, ABCDE.Length, ABCDE.Text, ABCDE.HashCS, 435475931745)]
    [InlineData(6, ABCDEF.Length, ABCDEF.Text, ABCDEF.HashCS, 112585661964897)]
    [InlineData(7, ABCDEFG.Length, ABCDEFG.Text, ABCDEFG.HashCS, 29104508263162465)]
    [InlineData(8, ABCDEFGH.Length, ABCDEFGH.Text, ABCDEFGH.HashCS, 7523094288207667809)]

    [InlineData(1, X.Length, X.Text, X.HashCS, 120)]
    [InlineData(2, XX.Length, XX.Text, XX.HashCS, 30840)]
    [InlineData(3, XXX.Length, XXX.Text, XXX.HashCS, 7895160)]
    [InlineData(4, XXXX.Length, XXXX.Text, XXXX.HashCS, 2021161080)]
    [InlineData(5, XXXXX.Length, XXXXX.Text, XXXXX.HashCS, 517417236600)]
    [InlineData(6, XXXXXX.Length, XXXXXX.Text, XXXXXX.HashCS, 132458812569720)]
    [InlineData(7, XXXXXXX.Length, XXXXXXX.Text, XXXXXXX.HashCS, 33909456017848440)]
    [InlineData(8, XXXXXXXX.Length, XXXXXXXX.Text, XXXXXXXX.HashCS, 8680820740569200760)]

    [InlineData(20, ABCDEFGHIJKLMNOPQRST.Length, ABCDEFGHIJKLMNOPQRST.Text, ABCDEFGHIJKLMNOPQRST.HashCS, 7523094288207667809)]

    // show that foo_bar is interpreted as foo-bar
    [InlineData(7, foo_bar.Length, foo_bar.Text, foo_bar.HashCS, 32195221641981798, "foo-bar", nameof(foo_bar))]
    [InlineData(7, foo_bar_hyphen.Length, foo_bar_hyphen.Text, foo_bar_hyphen.HashCS, 32195221641981798, "foo-bar", nameof(foo_bar_hyphen))]
    [InlineData(7, foo_bar_underscore.Length, foo_bar_underscore.Text, foo_bar_underscore.HashCS, 32195222480842598, "foo_bar", nameof(foo_bar_underscore))]
    public void validate(int expectedLength, int actualLength, string actualValue, long actualHash, long expectedHash, string? expectedValue = null, string originForDisambiguation = "")
    {
        _ = originForDisambiguation; // to allow otherwise-identical test data to coexist
        actualLength.Should().Be(expectedLength);
        actualHash.Should().Be(expectedHash);
        var bytes = Encoding.UTF8.GetBytes(actualValue);
        bytes.Length.Should().Be(expectedLength);
        AsciiHash.HashCS(bytes).Should().Be(expectedHash);
        AsciiHash.HashCS(actualValue.AsSpan()).Should().Be(expectedHash);

        if (expectedValue is not null)
        {
            actualValue.Should().Be(expectedValue);
        }
    }

    [Fact]
    public void ascii_hash_is_short()
    {
        ReadOnlySpan<byte> value = "abc"u8;
        var hash = AsciiHash.HashCS(value);
        hash.Should().Be(ABC.HashCS);
        ABC.IsCS(value, hash).Should().BeTrue();

        value = "abz"u8;
        hash = AsciiHash.HashCS(value);
        hash.Should().NotBe(ABC.HashCS);
        ABC.IsCS(value, hash).Should().BeFalse();
    }

    [Fact]
    public void ascii_hash_is_long()
    {
        ReadOnlySpan<byte> value = "abcdefghijklmnopqrst"u8;
        var hash = AsciiHash.HashCS(value);
        hash.Should().Be(ABCDEFGHIJKLMNOPQRST.HashCS);
        ABCDEFGHIJKLMNOPQRST.IsCS(value, hash).Should().BeTrue();

        value = "abcdefghijklmnopqrsz"u8;
        hash = AsciiHash.HashCS(value);
        hash.Should().Be(ABCDEFGHIJKLMNOPQRST.HashCS); // hash collision, fine
        ABCDEFGHIJKLMNOPQRST.IsCS(value, hash).Should().BeFalse();
    }

    // Test case-sensitive and case-insensitive equality for various lengths
    [Theory]
    [InlineData("a")] // length 1
    [InlineData("ab")] // length 2
    [InlineData("abc")] // length 3
    [InlineData("abcd")] // length 4
    [InlineData("abcde")] // length 5
    [InlineData("abcdef")] // length 6
    [InlineData("abcdefg")] // length 7
    [InlineData("abcdefgh")] // length 8
    [InlineData("abcdefghi")] // length 9
    [InlineData("abcdefghij")] // length 10
    [InlineData("abcdefghijklmnop")] // length 16
    [InlineData("abcdefghijklmnopqrst")] // length 20
    public void case_sensitive_equality(string text)
    {
        var lower = Encoding.UTF8.GetBytes(text);
        var upper = Encoding.UTF8.GetBytes(text.ToUpperInvariant());

        var hashLowerCS = AsciiHash.HashCS(lower);
        var hashUpperCS = AsciiHash.HashCS(upper);

        // Case-sensitive: same case should match
        AsciiHash.EqualsCS(lower, lower).Should().BeTrue("CS: lower == lower");
        AsciiHash.EqualsCS(upper, upper).Should().BeTrue("CS: upper == upper");

        // Case-sensitive: different case should NOT match
        AsciiHash.EqualsCS(lower, upper).Should().BeFalse("CS: lower != upper");
        AsciiHash.EqualsCS(upper, lower).Should().BeFalse("CS: upper != lower");

        // Hashes should be different for different cases
        hashUpperCS.Should().NotBe(hashLowerCS);
    }

    [Theory]
    [InlineData("a")] // length 1
    [InlineData("ab")] // length 2
    [InlineData("abc")] // length 3
    [InlineData("abcd")] // length 4
    [InlineData("abcde")] // length 5
    [InlineData("abcdef")] // length 6
    [InlineData("abcdefg")] // length 7
    [InlineData("abcdefgh")] // length 8
    [InlineData("abcdefghi")] // length 9
    [InlineData("abcdefghij")] // length 10
    [InlineData("abcdefghijklmnop")] // length 16
    [InlineData("abcdefghijklmnopqrst")] // length 20
    public void case_insensitive_equality(string text)
    {
        var lower = Encoding.UTF8.GetBytes(text);
        var upper = Encoding.UTF8.GetBytes(text.ToUpperInvariant());

        var hashLowerUC = AsciiHash.HashUC(lower);
        var hashUpperUC = AsciiHash.HashUC(upper);

        // Case-insensitive: same case should match
        AsciiHash.EqualsCI(lower, lower).Should().BeTrue("CI: lower == lower");
        AsciiHash.EqualsCI(upper, upper).Should().BeTrue("CI: upper == upper");

        // Case-insensitive: different case SHOULD match
        AsciiHash.EqualsCI(lower, upper).Should().BeTrue("CI: lower == upper");
        AsciiHash.EqualsCI(upper, lower).Should().BeTrue("CI: upper == lower");

        // CI hashes should be the same for different cases
        hashUpperUC.Should().Be(hashLowerUC);
    }

    [Theory]
    [InlineData("a")] // length 1
    [InlineData("ab")] // length 2
    [InlineData("abc")] // length 3
    [InlineData("abcd")] // length 4
    [InlineData("abcde")] // length 5
    [InlineData("abcdef")] // length 6
    [InlineData("abcdefg")] // length 7
    [InlineData("abcdefgh")] // length 8
    [InlineData("abcdefghi")] // length 9
    [InlineData("abcdefghij")] // length 10
    [InlineData("abcdefghijklmnop")] // length 16
    [InlineData("abcdefghijklmnopqrst")] // length 20
    public void case_insensitive_equality_mixed_bytes_and_chars(string text)
    {
        //Arrange
        var lowerChars = text.AsSpan();

        //Act
        var upperBytes = Encoding.UTF8.GetBytes(text.ToUpperInvariant());

        //Assert
        AsciiHash.EqualsCI(lowerChars, upperBytes).Should().BeTrue("CI: chars lower == bytes upper");
        AsciiHash.EqualsCI(upperBytes, lowerChars).Should().BeTrue("CI: bytes upper == chars lower");
        AsciiHash.SequenceEqualsCI(lowerChars, upperBytes).Should().BeTrue("CI sequence: chars lower == bytes upper");
        AsciiHash.SequenceEqualsCI(upperBytes, lowerChars).Should().BeTrue("CI sequence: bytes upper == chars lower");
        AsciiHash.EqualsCI((text + "x").AsSpan(), upperBytes).Should().BeFalse("CI: length mismatch");
    }

    [Theory]
    [InlineData("a")] // length 1
    [InlineData("ab")] // length 2
    [InlineData("abc")] // length 3
    [InlineData("abcd")] // length 4
    [InlineData("abcde")] // length 5
    [InlineData("abcdef")] // length 6
    [InlineData("abcdefg")] // length 7
    [InlineData("abcdefgh")] // length 8
    [InlineData("abcdefghi")] // length 9
    [InlineData("abcdefghij")] // length 10
    [InlineData("abcdefghijklmnop")] // length 16
    [InlineData("abcdefghijklmnopqrst")] // length 20
    [InlineData("foo-bar")] // foo_bar_hyphen
    [InlineData("foo_bar")] // foo_bar_underscore
    public void generated_types_case_sensitive(string text)
    {
        var lower = Encoding.UTF8.GetBytes(text);
        var upper = Encoding.UTF8.GetBytes(text.ToUpperInvariant());

        var hashLowerCS = AsciiHash.HashCS(lower);
        var hashUpperCS = AsciiHash.HashCS(upper);

        // Use the generated types to verify CS behavior
        switch (text)
        {
            case "a":
                A.IsCS(lower, hashLowerCS).Should().BeTrue();
                A.IsCS(lower, hashUpperCS).Should().BeFalse();
                break;
            case "ab":
                AB.IsCS(lower, hashLowerCS).Should().BeTrue();
                AB.IsCS(lower, hashUpperCS).Should().BeFalse();
                break;
            case "abc":
                ABC.IsCS(lower, hashLowerCS).Should().BeTrue();
                ABC.IsCS(lower, hashUpperCS).Should().BeFalse();
                break;
            case "abcd":
                ABCD.IsCS(lower, hashLowerCS).Should().BeTrue();
                ABCD.IsCS(lower, hashUpperCS).Should().BeFalse();
                break;
            case "abcde":
                ABCDE.IsCS(lower, hashLowerCS).Should().BeTrue();
                ABCDE.IsCS(lower, hashUpperCS).Should().BeFalse();
                break;
            case "abcdef":
                ABCDEF.IsCS(lower, hashLowerCS).Should().BeTrue();
                ABCDEF.IsCS(lower, hashUpperCS).Should().BeFalse();
                break;
            case "abcdefg":
                ABCDEFG.IsCS(lower, hashLowerCS).Should().BeTrue();
                ABCDEFG.IsCS(lower, hashUpperCS).Should().BeFalse();
                break;
            case "abcdefgh":
                ABCDEFGH.IsCS(lower, hashLowerCS).Should().BeTrue();
                ABCDEFGH.IsCS(lower, hashUpperCS).Should().BeFalse();
                break;
            case "abcdefghijklmnopqrst":
                ABCDEFGHIJKLMNOPQRST.IsCS(lower, hashLowerCS).Should().BeTrue();
                ABCDEFGHIJKLMNOPQRST.IsCS(lower, hashUpperCS).Should().BeFalse();
                break;
            case "foo-bar":
                foo_bar_hyphen.IsCS(lower, hashLowerCS).Should().BeTrue();
                foo_bar_hyphen.IsCS(lower, hashUpperCS).Should().BeFalse();
                break;
            case "foo_bar":
                foo_bar_underscore.IsCS(lower, hashLowerCS).Should().BeTrue();
                foo_bar_underscore.IsCS(lower, hashUpperCS).Should().BeFalse();
                break;
        }
    }

    [Theory]
    [InlineData("a")] // length 1
    [InlineData("ab")] // length 2
    [InlineData("abc")] // length 3
    [InlineData("abcd")] // length 4
    [InlineData("abcde")] // length 5
    [InlineData("abcdef")] // length 6
    [InlineData("abcdefg")] // length 7
    [InlineData("abcdefgh")] // length 8
    [InlineData("abcdefghi")] // length 9
    [InlineData("abcdefghij")] // length 10
    [InlineData("abcdefghijklmnop")] // length 16
    [InlineData("abcdefghijklmnopqrst")] // length 20
    [InlineData("foo-bar")] // foo_bar_hyphen
    [InlineData("foo_bar")] // foo_bar_underscore
    public void generated_types_case_insensitive(string text)
    {
        var lower = Encoding.UTF8.GetBytes(text);
        var upper = Encoding.UTF8.GetBytes(text.ToUpperInvariant());

        var hashLowerUC = AsciiHash.HashUC(lower);
        var hashUpperUC = AsciiHash.HashUC(upper);

        // Use the generated types to verify CI behavior
        switch (text)
        {
            case "a":
                A.IsCI(lower, hashLowerUC).Should().BeTrue();
                A.IsCI(upper, hashUpperUC).Should().BeTrue();
                break;
            case "ab":
                AB.IsCI(lower, hashLowerUC).Should().BeTrue();
                AB.IsCI(upper, hashUpperUC).Should().BeTrue();
                break;
            case "abc":
                ABC.IsCI(lower, hashLowerUC).Should().BeTrue();
                ABC.IsCI(upper, hashUpperUC).Should().BeTrue();
                break;
            case "abcd":
                ABCD.IsCI(lower, hashLowerUC).Should().BeTrue();
                ABCD.IsCI(upper, hashUpperUC).Should().BeTrue();
                break;
            case "abcde":
                ABCDE.IsCI(lower, hashLowerUC).Should().BeTrue();
                ABCDE.IsCI(upper, hashUpperUC).Should().BeTrue();
                break;
            case "abcdef":
                ABCDEF.IsCI(lower, hashLowerUC).Should().BeTrue();
                ABCDEF.IsCI(upper, hashUpperUC).Should().BeTrue();
                break;
            case "abcdefg":
                ABCDEFG.IsCI(lower, hashLowerUC).Should().BeTrue();
                ABCDEFG.IsCI(upper, hashUpperUC).Should().BeTrue();
                break;
            case "abcdefgh":
                ABCDEFGH.IsCI(lower, hashLowerUC).Should().BeTrue();
                ABCDEFGH.IsCI(upper, hashUpperUC).Should().BeTrue();
                break;
            case "abcdefghijklmnopqrst":
                ABCDEFGHIJKLMNOPQRST.IsCI(lower, hashLowerUC).Should().BeTrue();
                ABCDEFGHIJKLMNOPQRST.IsCI(upper, hashUpperUC).Should().BeTrue();
                break;
            case "foo-bar":
                foo_bar_hyphen.IsCI(lower, hashLowerUC).Should().BeTrue();
                foo_bar_hyphen.IsCI(upper, hashUpperUC).Should().BeTrue();
                break;
            case "foo_bar":
                foo_bar_underscore.IsCI(lower, hashLowerUC).Should().BeTrue();
                foo_bar_underscore.IsCI(upper, hashUpperUC).Should().BeTrue();
                break;
        }
    }

    // Test each generated AsciiHash type individually for case sensitivity
    [Fact]
    public void generated_type_a_case_sensitivity()
    {
        //Arrange
        ReadOnlySpan<byte> lower = "a"u8;

        //Act
        ReadOnlySpan<byte> upper = "A"u8;

        //Assert
        A.IsCS(lower, AsciiHash.HashCS(lower)).Should().BeTrue();
        A.IsCS(upper, AsciiHash.HashCS(upper)).Should().BeFalse();
        A.IsCI(lower, AsciiHash.HashUC(lower)).Should().BeTrue();
        A.IsCI(upper, AsciiHash.HashUC(upper)).Should().BeTrue();
    }

    [Fact]
    public void generated_type_ab_case_sensitivity()
    {
        //Arrange
        ReadOnlySpan<byte> lower = "ab"u8;

        //Act
        ReadOnlySpan<byte> upper = "AB"u8;

        //Assert
        AB.IsCS(lower, AsciiHash.HashCS(lower)).Should().BeTrue();
        AB.IsCS(upper, AsciiHash.HashCS(upper)).Should().BeFalse();
        AB.IsCI(lower, AsciiHash.HashUC(lower)).Should().BeTrue();
        AB.IsCI(upper, AsciiHash.HashUC(upper)).Should().BeTrue();
    }

    [Fact]
    public void generated_type_abc_case_sensitivity()
    {
        //Arrange
        ReadOnlySpan<byte> lower = "abc"u8;

        //Act
        ReadOnlySpan<byte> upper = "ABC"u8;

        //Assert
        ABC.IsCS(lower, AsciiHash.HashCS(lower)).Should().BeTrue();
        ABC.IsCS(upper, AsciiHash.HashCS(upper)).Should().BeFalse();
        ABC.IsCI(lower, AsciiHash.HashUC(lower)).Should().BeTrue();
        ABC.IsCI(upper, AsciiHash.HashUC(upper)).Should().BeTrue();
    }

    [Fact]
    public void generated_type_abcd_case_sensitivity()
    {
        //Arrange
        ReadOnlySpan<byte> lower = "abcd"u8;

        //Act
        ReadOnlySpan<byte> upper = "ABCD"u8;

        //Assert
        ABCD.IsCS(lower, AsciiHash.HashCS(lower)).Should().BeTrue();
        ABCD.IsCS(upper, AsciiHash.HashCS(upper)).Should().BeFalse();
        ABCD.IsCI(lower, AsciiHash.HashUC(lower)).Should().BeTrue();
        ABCD.IsCI(upper, AsciiHash.HashUC(upper)).Should().BeTrue();
    }

    [Fact]
    public void generated_type_abcde_case_sensitivity()
    {
        //Arrange
        ReadOnlySpan<byte> lower = "abcde"u8;

        //Act
        ReadOnlySpan<byte> upper = "ABCDE"u8;

        //Assert
        ABCDE.IsCS(lower, AsciiHash.HashCS(lower)).Should().BeTrue();
        ABCDE.IsCS(upper, AsciiHash.HashCS(upper)).Should().BeFalse();
        ABCDE.IsCI(lower, AsciiHash.HashUC(lower)).Should().BeTrue();
        ABCDE.IsCI(upper, AsciiHash.HashUC(upper)).Should().BeTrue();
    }

    [Fact]
    public void generated_type_abcdef_case_sensitivity()
    {
        //Arrange
        ReadOnlySpan<byte> lower = "abcdef"u8;

        //Act
        ReadOnlySpan<byte> upper = "ABCDEF"u8;

        //Assert
        ABCDEF.IsCS(lower, AsciiHash.HashCS(lower)).Should().BeTrue();
        ABCDEF.IsCS(upper, AsciiHash.HashCS(upper)).Should().BeFalse();
        ABCDEF.IsCI(lower, AsciiHash.HashUC(lower)).Should().BeTrue();
        ABCDEF.IsCI(upper, AsciiHash.HashUC(upper)).Should().BeTrue();
    }

    [Fact]
    public void generated_type_abcdefg_case_sensitivity()
    {
        //Arrange
        ReadOnlySpan<byte> lower = "abcdefg"u8;

        //Act
        ReadOnlySpan<byte> upper = "ABCDEFG"u8;

        //Assert
        ABCDEFG.IsCS(lower, AsciiHash.HashCS(lower)).Should().BeTrue();
        ABCDEFG.IsCS(upper, AsciiHash.HashCS(upper)).Should().BeFalse();
        ABCDEFG.IsCI(lower, AsciiHash.HashUC(lower)).Should().BeTrue();
        ABCDEFG.IsCI(upper, AsciiHash.HashUC(upper)).Should().BeTrue();
    }

    [Fact]
    public void generated_type_abcdefgh_case_sensitivity()
    {
        //Arrange
        ReadOnlySpan<byte> lower = "abcdefgh"u8;

        //Act
        ReadOnlySpan<byte> upper = "ABCDEFGH"u8;

        //Assert
        ABCDEFGH.IsCS(lower, AsciiHash.HashCS(lower)).Should().BeTrue();
        ABCDEFGH.IsCS(upper, AsciiHash.HashCS(upper)).Should().BeFalse();
        ABCDEFGH.IsCI(lower, AsciiHash.HashUC(lower)).Should().BeTrue();
        ABCDEFGH.IsCI(upper, AsciiHash.HashUC(upper)).Should().BeTrue();
    }

    [Fact]
    public void generated_type_abcdefghijklmnopqrst_case_sensitivity()
    {
        //Arrange
        ReadOnlySpan<byte> lower = "abcdefghijklmnopqrst"u8;

        //Act
        ReadOnlySpan<byte> upper = "ABCDEFGHIJKLMNOPQRST"u8;

        //Assert
        ABCDEFGHIJKLMNOPQRST.IsCS(lower, AsciiHash.HashCS(lower)).Should().BeTrue();
        ABCDEFGHIJKLMNOPQRST.IsCS(upper, AsciiHash.HashCS(upper)).Should().BeFalse();
        ABCDEFGHIJKLMNOPQRST.IsCI(lower, AsciiHash.HashUC(lower)).Should().BeTrue();
        ABCDEFGHIJKLMNOPQRST.IsCI(upper, AsciiHash.HashUC(upper)).Should().BeTrue();
    }

    [Fact]
    public void generated_type_foo_bar_case_sensitivity()
    {
        // foo_bar is interpreted as foo-bar
        ReadOnlySpan<byte> lower = "foo-bar"u8;
        ReadOnlySpan<byte> upper = "FOO-BAR"u8;

        foo_bar.IsCS(lower, AsciiHash.HashCS(lower)).Should().BeTrue();
        foo_bar.IsCS(upper, AsciiHash.HashCS(upper)).Should().BeFalse();
        foo_bar.IsCI(lower, AsciiHash.HashUC(lower)).Should().BeTrue();
        foo_bar.IsCI(upper, AsciiHash.HashUC(upper)).Should().BeTrue();
    }

    [Fact]
    public void generated_type_foo_bar_hyphen_case_sensitivity()
    {
        // foo_bar_hyphen is explicitly "foo-bar"
        ReadOnlySpan<byte> lower = "foo-bar"u8;
        ReadOnlySpan<byte> upper = "FOO-BAR"u8;

        foo_bar_hyphen.IsCS(lower, AsciiHash.HashCS(lower)).Should().BeTrue();
        foo_bar_hyphen.IsCS(upper, AsciiHash.HashCS(upper)).Should().BeFalse();
        foo_bar_hyphen.IsCI(lower, AsciiHash.HashUC(lower)).Should().BeTrue();
        foo_bar_hyphen.IsCI(upper, AsciiHash.HashUC(upper)).Should().BeTrue();
    }

    //The 17 helper types below were lower-case in upstream (class a, ab, ... x, xx, ...), which
    //raises CS8981 ("only contains lower-cased ascii characters"). They are upper-cased here and
    //given the payload they used to imply as an explicit [AsciiHash(...)] argument, so every Text,
    //Length and hash under test is byte-for-byte what it was. foo_bar and the CJK type keep the
    //inferred form, which is what covers name-derived payloads.
    [AsciiHash("a")] private static partial class A { }
    [AsciiHash("ab")] private static partial class AB { }
    [AsciiHash("abc")] private static partial class ABC { }
    [AsciiHash("abcd")] private static partial class ABCD { }
    [AsciiHash("abcde")] private static partial class ABCDE { }
    [AsciiHash("abcdef")] private static partial class ABCDEF { }
    [AsciiHash("abcdefg")] private static partial class ABCDEFG { }
    [AsciiHash("abcdefgh")] private static partial class ABCDEFGH { }

    [AsciiHash("abcdefghijklmnopqrst")] private static partial class ABCDEFGHIJKLMNOPQRST { }

    // show that foo_bar and foo-bar are different
    [AsciiHash] private static partial class foo_bar { }
    [AsciiHash("foo-bar")] private static partial class foo_bar_hyphen { }
    [AsciiHash("foo_bar")] private static partial class foo_bar_underscore { }

    [AsciiHash] private static partial class 窓 { }

    [AsciiHash("x")] private static partial class X { }
    [AsciiHash("xx")] private static partial class XX { }
    [AsciiHash("xxx")] private static partial class XXX { }
    [AsciiHash("xxxx")] private static partial class XXXX { }
    [AsciiHash("xxxxx")] private static partial class XXXXX { }
    [AsciiHash("xxxxxx")] private static partial class XXXXXX { }
    [AsciiHash("xxxxxxx")] private static partial class XXXXXXX { }
    [AsciiHash("xxxxxxxx")] private static partial class XXXXXXXX { }
}
