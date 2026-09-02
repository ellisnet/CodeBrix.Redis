using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class FormatTests(ITestOutputHelper output) : TestBase(output)
{
    public static IEnumerable<object?[]> EndpointData()
    {
        // note: the 3rd arg is for formatting; null means "expect the original string"

        // DNS
        yield return new object?[] { "localhost", new DnsEndPoint("localhost", 0), null };
        yield return new object?[] { "localhost:6390", new DnsEndPoint("localhost", 6390), null };
        yield return new object?[] { "bob.the.builder.com", new DnsEndPoint("bob.the.builder.com", 0), null };
        yield return new object?[] { "bob.the.builder.com:6390", new DnsEndPoint("bob.the.builder.com", 6390), null };
        // IPv4
        yield return new object?[] { "0.0.0.0", new IPEndPoint(IPAddress.Parse("0.0.0.0"), 0), null };
        yield return new object?[] { "127.0.0.1", new IPEndPoint(IPAddress.Parse("127.0.0.1"), 0), null };
        yield return new object?[] { "127.1", new IPEndPoint(IPAddress.Parse("127.1"), 0), "127.0.0.1" };
        yield return new object?[] { "127.1:6389", new IPEndPoint(IPAddress.Parse("127.1"), 6389), "127.0.0.1:6389" };
        yield return new object?[] { "127.0.0.1:6389", new IPEndPoint(IPAddress.Parse("127.0.0.1"), 6389), null };
        yield return new object?[] { "127.0.0.1:1", new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), null };
        yield return new object?[] { "127.0.0.1:2", new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2), null };
        yield return new object?[] { "10.10.9.18:2", new IPEndPoint(IPAddress.Parse("10.10.9.18"), 2), null };
        // IPv6
        yield return new object?[] { "::1", new IPEndPoint(IPAddress.Parse("::1"), 0), null };
        yield return new object?[] { "::1:6379", new IPEndPoint(IPAddress.Parse("::0.1.99.121"), 0), "::0.1.99.121" }; // remember your brackets!
        yield return new object?[] { "[::1]:6379", new IPEndPoint(IPAddress.Parse("::1"), 6379), null };
        yield return new object?[] { "[::1]", new IPEndPoint(IPAddress.Parse("::1"), 0), "::1" };
        yield return new object?[] { "[::1]:1000", new IPEndPoint(IPAddress.Parse("::1"), 1000), null };
        yield return new object?[] { "2001:db7:85a3:8d2:1319:8a2e:370:7348", new IPEndPoint(IPAddress.Parse("2001:db7:85a3:8d2:1319:8a2e:370:7348"), 0), null };
        yield return new object?[] { "[2001:db7:85a3:8d2:1319:8a2e:370:7348]", new IPEndPoint(IPAddress.Parse("2001:db7:85a3:8d2:1319:8a2e:370:7348"), 0), "2001:db7:85a3:8d2:1319:8a2e:370:7348" };
        yield return new object?[] { "[2001:db7:85a3:8d2:1319:8a2e:370:7348]:1000", new IPEndPoint(IPAddress.Parse("2001:db7:85a3:8d2:1319:8a2e:370:7348"), 1000), null };
    }

    [Theory]
    [MemberData(nameof(EndpointData))]
    public void parse_end_point(string data, EndPoint expected, string? expectedFormat)
    {
        Format.TryParseEndPoint(data, out var result).Should().BeTrue();
        result.Should().Be(expected);

        // and write again
        var s = Format.ToString(result);
        expectedFormat ??= data;
        s.Should().Be(expectedFormat);
    }

    // UDS endpoints live outside EndpointData because UnixDomainSocketEndPoint does not implement
    // value equality — these compare via ToString instead.
    [Fact]
    public void parse_unix_domain_socket_end_point()
    {
        Format.TryParseEndPoint("!/tmp/redis.sock", out var ep).Should().BeTrue();
        var uds = Assert.IsType<System.Net.Sockets.UnixDomainSocketEndPoint>(ep);
        uds.ToString().Should().Be("/tmp/redis.sock");
        Format.ToString(ep).Should().Be("!/tmp/redis.sock");
    }

    [Fact]
    public void parse_abstract_unix_domain_socket_end_point()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the abstract socket namespace is Linux-only");

        // "!@name": socat/systemd '@' convention for the Linux abstract namespace. The parse maps it
        // to the kernel's leading-NUL spelling; UnixDomainSocketEndPoint.ToString renders that back
        // as '@name', so the config string round-trips exactly.
        Format.TryParseEndPoint("!@redis-abstract", out var ep).Should().BeTrue();
        var uds = Assert.IsType<System.Net.Sockets.UnixDomainSocketEndPoint>(ep);
        uds.ToString().Should().Be("@redis-abstract");
        Format.ToString(ep).Should().Be("!@redis-abstract");
    }

    [Theory]
    [InlineData(CommandFlags.None, "None")]
    [InlineData(CommandFlags.PreferReplica, "PreferReplica")] // 2-bit flag is hit-and-miss
    [InlineData(CommandFlags.DemandReplica, "DemandReplica")] // 2-bit flag is hit-and-miss

    [InlineData(CommandFlags.PreferReplica | CommandFlags.FireAndForget, "FireAndForget, PreferReplica")] // 2-bit flag is hit-and-miss
    [InlineData(CommandFlags.DemandReplica | CommandFlags.FireAndForget, "FireAndForget, DemandReplica")] // 2-bit flag is hit-and-miss
    public void command_flags_formatting(CommandFlags value, string expected)
    {
        Assert.SkipWhen(Runtime.IsMono, "Mono has different enum flag behavior");
        value.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData(ClientType.Normal, "Normal")]
    [InlineData(ClientType.Replica, "Replica")]
    [InlineData(ClientType.PubSub, "PubSub")]
    public void client_type_formatting(ClientType value, string expected)
        => value.ToString().Should().Be(expected);

    [Theory]
    [InlineData(ClientFlags.None, "None")]
    [InlineData(ClientFlags.Replica | ClientFlags.Transaction, "Replica, Transaction")]
    [InlineData(ClientFlags.Transaction | ClientFlags.ReplicaMonitor | ClientFlags.UnixDomainSocket, "ReplicaMonitor, Transaction, UnixDomainSocket")]
    public void client_flags_formatting(ClientFlags value, string expected)
        => value.ToString().Should().Be(expected);

    [Theory]
    [InlineData(ReplicationChangeOptions.None, "None")]
    [InlineData(ReplicationChangeOptions.ReplicateToOtherEndpoints, "ReplicateToOtherEndpoints")]
    [InlineData(ReplicationChangeOptions.SetTiebreaker | ReplicationChangeOptions.ReplicateToOtherEndpoints, "SetTiebreaker, ReplicateToOtherEndpoints")]
    [InlineData(ReplicationChangeOptions.Broadcast | ReplicationChangeOptions.SetTiebreaker | ReplicationChangeOptions.ReplicateToOtherEndpoints, "All")]
    public void replication_change_options_formatting(ReplicationChangeOptions value, string expected)
        => value.ToString().Should().Be(expected);

    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1")]
    [InlineData(-1, "-1")]
    [InlineData(100, "100")]
    [InlineData(-100, "-100")]
    [InlineData(int.MaxValue, "2147483647")]
    [InlineData(int.MinValue, "-2147483648")]
    public unsafe void format_int32(int value, string expectedValue)
    {
        Span<byte> dest = stackalloc byte[expectedValue.Length];
        Format.FormatInt32(value, dest).Should().Be(expectedValue.Length);
        fixed (byte* s = dest)
        {
            Encoding.ASCII.GetString(s, expectedValue.Length).Should().Be(expectedValue);
        }
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1")]
    [InlineData(-1, "-1")]
    [InlineData(100, "100")]
    [InlineData(-100, "-100")]
    [InlineData(long.MaxValue, "9223372036854775807")]
    [InlineData(long.MinValue, "-9223372036854775808")]
    public unsafe void format_int64(long value, string expectedValue)
    {
        Format.MeasureInt64(value).Should().Be(expectedValue.Length);
        Span<byte> dest = stackalloc byte[expectedValue.Length];
        Format.FormatInt64(value, dest).Should().Be(expectedValue.Length);
        fixed (byte* s = dest)
        {
            Encoding.ASCII.GetString(s, expectedValue.Length).Should().Be(expectedValue);
        }
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1")]
    [InlineData(100, "100")]
    [InlineData(ulong.MaxValue, "18446744073709551615")]
    public unsafe void format_u_int64(ulong value, string expectedValue)
    {
        Format.MeasureUInt64(value).Should().Be(expectedValue.Length);
        Span<byte> dest = stackalloc byte[expectedValue.Length];
        Format.FormatUInt64(value, dest).Should().Be(expectedValue.Length);
        fixed (byte* s = dest)
        {
            Encoding.ASCII.GetString(s, expectedValue.Length).Should().Be(expectedValue);
        }
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1")]
    [InlineData(-1, "-1")]
    [InlineData(0.5, "0.5")]
    [InlineData(0.50001, "0.50000999999999995")]
    [InlineData(Math.PI, "3.1415926535897931")]
    [InlineData(100, "100")]
    [InlineData(-100, "-100")]
    [InlineData(double.MaxValue, "1.7976931348623157E+308")]
    [InlineData(double.MinValue, "-1.7976931348623157E+308")]
    [InlineData(double.Epsilon, "4.9406564584124654E-324")]
    [InlineData(double.PositiveInfinity, "+inf")]
    [InlineData(double.NegativeInfinity, "-inf")]
    [InlineData(double.NaN, "NaN")] // never used in normal code

    public unsafe void format_double(double value, string expectedValue)
    {
        Format.MeasureDouble(value).Should().Be(expectedValue.Length);
        Span<byte> dest = stackalloc byte[expectedValue.Length];
        Format.FormatDouble(value, dest).Should().Be(expectedValue.Length);
        fixed (byte* s = dest)
        {
            Encoding.ASCII.GetString(s, expectedValue.Length).Should().Be(expectedValue);
        }
    }
}
