using System;
using SilverAssertions;
using Xunit;
using Xunit.Sdk;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

// who watches the watchers?
public class TestHarnessTests
{
    // this bit isn't required, but: by subclassing TestHarness we can expose the idiomatic test-framework faults.
    private sealed class XUnitTestHarness(CommandMap? commandMap = null, RedisChannel channelPrefix = default, RedisKey keyPrefix = default)
        : TestHarness(commandMap,  channelPrefix, keyPrefix)
    {
        protected override void OnValidateFail(string expected, string actual)
            => Assert.Equal(expected, actual);

        protected override void OnValidateFail(ReadOnlyMemory<byte> expected, ReadOnlyMemory<byte> actual)
            => Assert.Equal(expected, actual);

        protected override void OnValidateFail(in RedisKey expected, in RedisKey actual)
            => Assert.Equal(expected, actual);
    }

    [Fact]
    public void basic_write_bytes()
    {
        var resp = new XUnitTestHarness();
        resp.ValidateRouting(RedisKey.Null, "hello world");
        resp.ValidateResp(
            "*2\r\n$4\r\nECHO\r\n$11\r\nhello world\r\n"u8,
            "echo",
            "hello world");
    }
    [Fact]
    public void basic_write_string()
    {
        var resp = new XUnitTestHarness();
        resp.ValidateRouting(RedisKey.Null, "hello world");
        resp.ValidateResp(
            "*2\r\n$4\r\nECHO\r\n$11\r\nhello world\r\n",
            "echo",
            "hello world");
    }

    [Fact]
    public void with_key_prefix()
    {
        var map = CommandMap.Create(new() { ["sEt"] = "put" });
        RedisKey key = "mykey";
        var resp = new XUnitTestHarness(keyPrefix: "123/", commandMap: map);
        object[] args = { key, 42 };
        resp.ValidateRouting(key, args);
        resp.ValidateResp("*3\r\n$3\r\nPUT\r\n$9\r\n123/mykey\r\n$2\r\n42\r\n", "set", args);
    }

    [Fact]
    public void command_map_deltas()
    {
        CommandMap.Default.ToString().Should().Be("");
        CommandMap.Create(new() { ["sEt"] = "set" }).ToString().Should().Be("");
        CommandMap.Create(new() { ["sEt"] = "put" }).ToString().Should().Be("$SET=PUT");
        CommandMap.Create(new() { "echo" }, available: false).ToString().Should().Be("$ECHO=");
    }

    [Fact]
    public void with_key_prefix_detect_incorrect_usage()
    {
        string key = "mykey"; // incorrectly not a key
        var resp = new XUnitTestHarness(keyPrefix: "123/");
        object[] args = { key, 42 };
        var ex = Assert.Throws<EqualException>(() => resp.ValidateRouting(key, args));
        ex.Message.Should().Contain("Expected: 123/mykey");
        ex.Message.Should().Contain("Actual:   (null)");

        ex = Assert.Throws<EqualException>(() => resp.ValidateResp("*3\r\n$3\r\nSET\r\n$9\r\n123/mykey\r\n$2\r\n42\r\n", "set", args));
        ex.Message.Should().Contain(@"Expected: ""*3\r\n$3\r\nSET\r\n$9\r\n123/mykey\r\n$2\r\n42\r\n""");
        ex.Message.Should().Contain(@"Actual:   ""*3\r\n$3\r\nSET\r\n$5\r\nmykey\r\n$2\r\n42\r\n""");
    }

    [Fact]
    public void parse_example()
    {
        var resp = new XUnitTestHarness();
        var result = resp.Read("*3\r\n:42\r\n#t\r\n$3\r\nabc\r\n"u8);
        result.Length.Should().Be(3);
        ((int)result[0]).Should().Be(42);
        ((bool)result[1]).Should().BeTrue();
        ((string?)result[2]).Should().Be("abc");
    }
}
