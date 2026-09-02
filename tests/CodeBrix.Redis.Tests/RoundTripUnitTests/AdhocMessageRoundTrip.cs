using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.RoundTripUnitTests; //was previously: StackExchange.Redis.Tests.RoundTripUnitTests;

public class AdHocMessageRoundTrip(ITestOutputHelper log)
{
    public enum MapMode
    {
        Null,
        Default,
        Disabled,
        Renamed,
    }

    [Theory(Timeout = 1000)]
    [InlineData(MapMode.Null, "", "*1\r\n$4\r\nECHO\r\n")]
    [InlineData(MapMode.Default, "", "*1\r\n$4\r\nECHO\r\n")]
    [InlineData(MapMode.Disabled, "", "")]
    [InlineData(MapMode.Renamed, "", "*1\r\n$5\r\nECHO2\r\n")]
    [InlineData(MapMode.Null, "hello", "*2\r\n$4\r\nECHO\r\n$5\r\nhello\r\n")]
    [InlineData(MapMode.Default, "hello", "*2\r\n$4\r\nECHO\r\n$5\r\nhello\r\n")]
    [InlineData(MapMode.Disabled, "hello", "")]
    [InlineData(MapMode.Renamed, "hello", "*2\r\n$5\r\nECHO2\r\n$5\r\nhello\r\n")]
    public async Task echo_round_trip_test(MapMode mode, string payload, string requestResp)
    {
        var map = GetMap(mode);

        object[] args = string.IsNullOrEmpty(payload) ? [] : [payload];
        if (mode is MapMode.Disabled)
        {
            var ex = Assert.Throws<RedisCommandException>(() => new RedisDatabase.ExecuteMessage(map, -1, CommandFlags.None, "echo", args));
            "This operation has been disabled in the command-map and cannot be used: echo".Should().StartWith(ex.Message);
        }
        else
        {
            var msg = new RedisDatabase.ExecuteMessage(map, -1, CommandFlags.None, "echo", args);
            msg.Command.Should().Be(RedisCommand.ECHO); // in v3: this is recognized correctly

            msg.CommandAndKey.Should().Be("ECHO");
            msg.CommandString.Should().Be("ECHO");
            var result =
                await TestConnection.ExecuteAsync(msg, ResultProcessor.ScriptResult, requestResp, ":5\r\n", commandMap: map, log: log, cancellationToken: TestContext.Current.CancellationToken);
            result.Resp3Type.Should().Be(ResultType.Integer);
            result.AsInt32().Should().Be(5);
        }
    }

    //was previously: [Theory(Timeout = 1000)]. This test is synchronous and has no cancellable
    //call, so a timeout could never interrupt it (xUnit1069) - the attribute was inert. The test
    //itself is unchanged.
    [Theory]
    [InlineData("ACL SETUSER x")]
    [InlineData("get key")]
    public void command_with_whitespace_throws(string command)
    {
        object[] args = [];
        var ex = Assert.Throws<RedisCommandException>(
            () => new RedisDatabase.ExecuteMessage(CommandMap.Default, -1, CommandFlags.None, command, args));
        ex.Message.Should().Contain("whitespace");
    }

    //was previously: [Fact(Timeout = 1000)]; synchronous, see the note above.
    [Fact]
    public void single_token_command_does_not_throw()
    {
        // the correct token-per-argument form must still be accepted unchanged
        object[] args = ["SETUSER", "x"];
        var msg = new RedisDatabase.ExecuteMessage(CommandMap.Default, -1, CommandFlags.None, "ACL", args);
        msg.CommandString.Should().Be("ACL");
    }

    private static CommandMap? GetMap(MapMode mode) => mode switch
    {
        MapMode.Null => null,
        MapMode.Default => CommandMap.Default,
        MapMode.Disabled => CommandMap.Create(new HashSet<string> { "echo", "custom" }, available: false),
        MapMode.Renamed => CommandMap.Create(new Dictionary<string, string?> { { "echo", "echo2" }, { "custom", "custom2" } }),
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    [Theory(Timeout = 1000)]
    [InlineData(MapMode.Null, "", "*1\r\n$6\r\nCUSTOM\r\n")]
    [InlineData(MapMode.Default, "", "*1\r\n$6\r\nCUSTOM\r\n")]
    // [InlineData(MapMode.Disabled, "", "")]
    // [InlineData(MapMode.Renamed, "", "*1\r\n$7\r\nCUSTOM2\r\n")]
    [InlineData(MapMode.Null, "hello", "*2\r\n$6\r\nCUSTOM\r\n$5\r\nhello\r\n")]
    [InlineData(MapMode.Default, "hello", "*2\r\n$6\r\nCUSTOM\r\n$5\r\nhello\r\n")]
    // [InlineData(MapMode.Disabled, "hello", "")]
    // [InlineData(MapMode.Renamed, "hello", "*2\r\n$7\r\nCUSTOM2\r\n$5\r\nhello\r\n")]
    public async Task CustomRoundTripTest(MapMode mode, string payload, string requestResp)
    {
        var map = GetMap(mode);

        object[] args = string.IsNullOrEmpty(payload) ? [] : [payload];
        if (mode is MapMode.Disabled)
        {
            var ex = Assert.Throws<RedisCommandException>(() => new RedisDatabase.ExecuteMessage(map, -1, CommandFlags.None, "custom", args));
            "This operation has been disabled in the command-map and cannot be used: custom".Should().StartWith(ex.Message);
        }
        else
        {
            var msg = new RedisDatabase.ExecuteMessage(map, -1, CommandFlags.None, "custom", args);
            msg.Command.Should().Be(RedisCommand.UNKNOWN);

            msg.CommandAndKey.Should().Be("custom");
            msg.CommandString.Should().Be("custom");
            var result =
                await TestConnection.ExecuteAsync(msg, ResultProcessor.ScriptResult, requestResp, ":5\r\n", commandMap: map, log: log, cancellationToken: TestContext.Current.CancellationToken);
            result.Resp3Type.Should().Be(ResultType.Integer);
            result.AsInt32().Should().Be(5);
        }
    }
}
