using System;
using System.Collections.Generic;
using System.Globalization;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class KeyAndValueTests
{
    [Fact]
    public void test_values()
    {
        RedisValue @default = default(RedisValue);
        CheckNull(@default);

        RedisValue nullString = (string?)null;
        CheckNull(nullString);

        RedisValue nullBlob = (byte[]?)null;
        CheckNull(nullBlob);

        RedisValue emptyString = "";
        CheckNotNull(emptyString);

        RedisValue emptyBlob = Array.Empty<byte>();
        CheckNotNull(emptyBlob);

        RedisValue a0 = new string('a', 1);
        CheckNotNull(a0);
        RedisValue a1 = new string('a', 1);
        CheckNotNull(a1);
        RedisValue b0 = new[] { (byte)'b' };
        CheckNotNull(b0);
        RedisValue b1 = new[] { (byte)'b' };
        CheckNotNull(b1);

        RedisValue i4 = 1;
        CheckNotNull(i4);
        RedisValue i8 = 1L;
        CheckNotNull(i8);

        RedisValue bool1 = true;
        CheckNotNull(bool1);
        RedisValue bool2 = false;
        CheckNotNull(bool2);
        RedisValue bool3 = true;
        CheckNotNull(bool3);

        CheckSame(a0, a0);
        CheckSame(a1, a1);
        CheckSame(a0, a1);

        CheckSame(b0, b0);
        CheckSame(b1, b1);
        CheckSame(b0, b1);

        CheckSame(i4, i4);
        CheckSame(i8, i8);
        CheckSame(i4, i8);

        CheckSame(bool1, bool3);
        CheckNotSame(bool1, bool2);
    }

    internal static void CheckSame(RedisValue x, RedisValue y)
    {
        if (x.TryParse(out double value) && double.IsNaN(value))
        {
            // NaN has atypical equality rules
            (y.TryParse(out value) && double.IsNaN(value)).Should().BeTrue();
            return;
        }
        Equals(x, y).Should().BeTrue("Equals(x, y)");
        Equals(y, x).Should().BeTrue("Equals(y, x)");
        EqualityComparer<RedisValue>.Default.Equals(x, y).Should().BeTrue("EQ(x,y)");
        EqualityComparer<RedisValue>.Default.Equals(y, x).Should().BeTrue("EQ(y,x)");
        (x == y).Should().BeTrue("x==y");
        (y == x).Should().BeTrue("y==x");
        (x != y).Should().BeFalse("x!=y");
        (y != x).Should().BeFalse("y!=x");
        x.Equals(y).Should().BeTrue("x.EQ(y)");
        y.Equals(x).Should().BeTrue("y.EQ(x)");
        (x.GetHashCode() == y.GetHashCode()).Should().BeTrue("GetHashCode");
    }

    private static void CheckNotSame(RedisValue x, RedisValue y)
    {
        Equals(x, y).Should().BeFalse();
        Equals(y, x).Should().BeFalse();
        EqualityComparer<RedisValue>.Default.Equals(x, y).Should().BeFalse();
        EqualityComparer<RedisValue>.Default.Equals(y, x).Should().BeFalse();
        (x == y).Should().BeFalse();
        (y == x).Should().BeFalse();
        (x != y).Should().BeTrue();
        (y != x).Should().BeTrue();
        x.Equals(y).Should().BeFalse();
        y.Equals(x).Should().BeFalse();
        (x.GetHashCode() == y.GetHashCode()).Should().BeFalse(); // well, very unlikely
    }

    private static void CheckNotNull(RedisValue value)
    {
        value.IsNull.Should().BeFalse();
        ((byte[]?)value).Should().NotBeNull();
        ((string?)value).Should().NotBeNull();
        value.GetHashCode().Should().NotBe(-1);

        ((string?)value).Should().NotBeNull();
        ((byte[]?)value).Should().NotBeNull();

        CheckSame(value, value);
        CheckNotSame(value, default(RedisValue));
        CheckNotSame(value, (string?)null);
        CheckNotSame(value, (byte[]?)null);
    }

    internal static void CheckNull(RedisValue value)
    {
        value.IsNull.Should().BeTrue();
        value.IsNullOrEmpty.Should().BeTrue();
        value.IsInteger.Should().BeFalse();
        value.GetHashCode().Should().Be(-1);

        ((string?)value).Should().BeNull();
        ((byte[]?)value).Should().BeNull();

        ((int)value).Should().Be(0);
        ((long)value).Should().Be(0L);

        CheckSame(value, value);
        // CheckSame(value, default(RedisValue));
        // CheckSame(value, (string)null);
        // CheckSame(value, (byte[])null);
    }

    [Fact]
    public void values_are_convertible()
    {
        //Arrange
        RedisValue val = 123;
        object o = val;
        byte[] blob = (byte[])Convert.ChangeType(o, typeof(byte[]));
        blob.Length.Should().Be(3);
        blob[0].Should().Be((byte)'1');
        blob[1].Should().Be((byte)'2');
        blob[2].Should().Be((byte)'3');
        Convert.ToDouble(o).Should().Be(123);
        IConvertible c = (IConvertible)o;
        // ReSharper disable RedundantCast
        c.ToInt16(CultureInfo.InvariantCulture).Should().Be((short)123);
        c.ToInt32(CultureInfo.InvariantCulture).Should().Be((int)123);
        c.ToInt64(CultureInfo.InvariantCulture).Should().Be(123L);
        c.ToSingle(CultureInfo.InvariantCulture).Should().Be(123F);
        c.ToString(CultureInfo.InvariantCulture).Should().Be("123");
        c.ToDouble(CultureInfo.InvariantCulture).Should().Be(123D);
        c.ToDecimal(CultureInfo.InvariantCulture).Should().Be(123M);
        c.ToUInt16(CultureInfo.InvariantCulture).Should().Be((ushort)123);
        c.ToUInt32(CultureInfo.InvariantCulture).Should().Be(123U);
        c.ToUInt64(CultureInfo.InvariantCulture).Should().Be(123UL);

        //Act
        blob = (byte[])c.ToType(typeof(byte[]), CultureInfo.InvariantCulture);

        //Assert
        blob.Length.Should().Be(3);
        blob[0].Should().Be((byte)'1');
        blob[1].Should().Be((byte)'2');
        blob[2].Should().Be((byte)'3');
    }

    [Fact]
    public void can_be_dynamic()
    {
        //Arrange
        RedisValue val = "abc";
        object o = val;
        dynamic d = o;

        //Act
        byte[] blob = (byte[])d;

        //Assert
        // could be in a try/catch
        blob.Length.Should().Be(3);
        blob[0].Should().Be((byte)'a');
        blob[1].Should().Be((byte)'b');
        blob[2].Should().Be((byte)'c');
    }
}
