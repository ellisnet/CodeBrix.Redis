using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

/// <summary>
/// Integration tests for MOVED-to-same-endpoint error handling.
/// When a MOVED error points to the same endpoint, the client should reconnect before retrying,
/// allowing the DNS record/proxy/load balancer to route to a different underlying server host.
/// </summary>
[RunPerProtocol]
public class MovedUnitTests(ITestOutputHelper log)
{
    private RedisKey Me([CallerMemberName] string callerName = "") => callerName;

    [Theory]
    /*
    [InlineData(ServerType.Cluster, WriteMode.Sync)]
    [InlineData(ServerType.Standalone, WriteMode.Sync)]
    [InlineData(ServerType.Cluster, WriteMode.Async)]
    [InlineData(ServerType.Standalone, WriteMode.Async)]
    */
    [InlineData(ServerType.Cluster, WriteMode.Pipe)]
    [InlineData(ServerType.Standalone, WriteMode.Pipe)]
    public async Task cross_slot_disallowed(ServerType serverType, WriteMode writeMode)
    {
        // intentionally sending as strings (not keys) via execute to prevent the
        // client library from getting in our way
        string keyA = "abc", keyB = "def"; // known to be on different slots

        using var server = new InProcessTestServer(log) { ServerType = serverType };
        await using var muxer = await server.ConnectAsync(writeMode: writeMode, withPubSub: false);

        var db = muxer.GetDatabase();
        await db.StringSetAsync(keyA, "value", flags: CommandFlags.FireAndForget);

        var pending = db.ExecuteAsync("rename", keyA, keyB);
        if (serverType == ServerType.Cluster)
        {
            var ex = await Assert.ThrowsAsync<RedisServerException>(() => pending);
            ex.Message.Should().Contain("CROSSSLOT");

            (await db.StringGetAsync(keyA)).Should().Be("value");
            (await db.KeyExistsAsync(keyB)).Should().BeFalse();
        }
        else
        {
            await pending;
            (await db.KeyExistsAsync(keyA)).Should().BeFalse();
            (await db.StringGetAsync(keyB)).Should().Be("value");
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task key_migration_followed(bool allowFollowRedirects, bool toNewUnknownNode)
    {
        RedisKey key = Me();
        using var server = new InProcessTestServer(log) { ServerType = ServerType.Cluster };
        // depending on the test, we might not want the client to know about the second node yet
        var secondNode = toNewUnknownNode ? null : server.AddEmptyNode();

        await using var muxer = await server.ConnectAsync();
        var db = muxer.GetDatabase();

        await db.StringSetAsync(key, "value");
        var value = await db.StringGetAsync(key);
        ((string?)value).Should().Be("value");

        if (toNewUnknownNode) // if deferred, the client doesn't know about this yet
        {
            secondNode = server.AddEmptyNode();
        }

        server.Migrate(key, secondNode);

        if (allowFollowRedirects)
        {
            value = await db.StringGetAsync(key, flags: CommandFlags.None);
            ((string?)value).Should().Be("value");
        }
        else
        {
            var ex = await Assert.ThrowsAsync<RedisServerException>(() => db.StringGetAsync(key, flags: CommandFlags.NoRedirect));
            ex.Message.Should().Contain("MOVED");
        }
    }

    /// <summary>
    /// Integration test: Verifies that when a MOVED error points to the same endpoint,
    /// the client reconnects and successfully retries the operation.
    ///
    /// Test scenario:
    /// 1. Client connects to test server
    /// 2. Client sends SET command for trigger key
    /// 3. Server returns MOVED error pointing to same endpoint
    /// 4. Client detects MOVED-to-same-endpoint and triggers reconnection
    /// 5. Client retries SET command after reconnection
    /// 6. Server processes SET normally on retry
    ///
    /// Expected behavior:
    /// - SET command count should increase by 2 (initial attempt + retry)
    /// - MOVED response count should increase by 1 (only on first attempt)
    /// - Connection count should increase by 1 (reconnection after MOVED)
    /// - Final SET operation should succeed with value stored.
    /// </summary>
    [Theory]
    [InlineData(ServerType.Cluster)]
    [InlineData(ServerType.Standalone)]
    public async Task moved_to_same_endpoint_triggers_reconnect_and_retry_command_succeeds(ServerType serverType)
    {
        RedisKey key = Me();

        using var testServer = new MovedTestServer(
            triggerKey: key,
            log: log) { ServerType = serverType, };

        // Act: Connect to the test server
        await using var conn = await testServer.ConnectAsync(withPubSub: false);
        // Ping the server to ensure it's responsive
        var server = conn.GetServer(testServer.DefaultEndPoint);

        var id = await server.ExecuteAsync("client", "id");
        log?.WriteLine($"Client id before: {id}");

        await server.PingAsync(); // init everything
        // Verify server is detected as per test config
        server.ServerType.Should().Be(serverType);
        var db = conn.GetDatabase();

        // Record baseline counters after initial connection
        testServer.SetCmdCount.Should().Be(0);
        testServer.MovedResponseCount.Should().Be(0);
        var initialConnectionCount = testServer.TotalClientCount;

        // Execute SET command: This should receive MOVED → reconnect → retry → succeed
        var setResult = await db.StringSetAsync(key, "testvalue");

        // Assert: Verify SET command succeeded
        setResult.Should().BeTrue("SET command should return true (OK)");

        // Verify the value was actually stored (proving retry succeeded)
        var retrievedValue = await db.StringGetAsync(key);
        ((string?)retrievedValue).Should().Be("testvalue");

        // Verify SET command was executed twice: once with MOVED response, once successfully
        testServer.SetCmdCount.Should().Be(2);

        // Verify MOVED response was returned exactly once
        testServer.MovedResponseCount.Should().Be(1);

        // Verify reconnection occurred: connection count should have increased by 1
        testServer.TotalClientCount.Should().Be(initialConnectionCount + 1);
        id = await server.ExecuteAsync("client", "id");
        log?.WriteLine($"Client id after: {id}");
    }

    /// <summary>
    /// Integration test: Verifies that batch commands issued during a MOVED-triggered
    /// reconnection are queued to the backlog and succeed after reconnection completes,
    /// rather than throwing NoConnectionAvailable immediately.
    ///
    /// Test scenario:
    /// 1. Client connects to test server
    /// 2. Client sends a batch containing SET commands, with the trigger key as the last command
    /// 3. Server returns MOVED error for the trigger key pointing to same endpoint
    /// 4. Client triggers reconnection and queues the MOVED command's retry in the backlog
    /// 5. All batch commands complete successfully after reconnection
    ///
    /// Expected behavior:
    /// - All batch tasks should complete successfully (no exceptions)
    /// - MOVED response count should be 1
    /// - Connection count should increase by 1 (reconnection after MOVED)
    /// - Values should be stored correctly
    /// </summary>
    [Theory]
    [InlineData(ServerType.Cluster)]
    [InlineData(ServerType.Standalone)]
    public async Task moved_to_same_endpoint_batch_commands_queued_during_reconnect(ServerType serverType)
    {
        RedisKey key = Me();

        using var testServer = new MovedTestServer(
            triggerKey: key,
            log: log) { ServerType = serverType, };

        await using var conn = await testServer.ConnectAsync(withPubSub: false);
        var server = conn.GetServer(testServer.DefaultEndPoint);
        await server.PingAsync(); // init everything
        server.ServerType.Should().Be(serverType);
        var db = conn.GetDatabase();

        // Record baseline counters
        testServer.SetCmdCount.Should().Be(0);
        testServer.MovedResponseCount.Should().Be(0);
        var initialConnectionCount = testServer.TotalClientCount;

        // Create a batch: normal commands + trigger key as last command
        var batch = db.CreateBatch();
        var setTask1 = batch.StringSetAsync("normalkey1", "value1");
        var setTask2 = batch.StringSetAsync("normalkey2", "value2");
        var triggerTask = batch.StringSetAsync(key, "triggervalue"); // this will get MOVED
        batch.Execute();

        // All tasks should complete successfully (trigger command retried after reconnect)
        (await setTask1).Should().BeTrue("First SET should succeed");
        (await setTask2).Should().BeTrue("Second SET should succeed");
        (await triggerTask).Should().BeTrue("Trigger SET should succeed after reconnect+retry");

        // Verify values were stored
        ((string?)await db.StringGetAsync("normalkey1")).Should().Be("value1");
        ((string?)await db.StringGetAsync("normalkey2")).Should().Be("value2");
        ((string?)await db.StringGetAsync(key)).Should().Be("triggervalue");

        // Verify MOVED was returned exactly once
        testServer.MovedResponseCount.Should().Be(1);

        // Verify reconnection occurred
        testServer.TotalClientCount.Should().Be(initialConnectionCount + 1);
    }
}
