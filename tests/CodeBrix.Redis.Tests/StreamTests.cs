using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[RunPerProtocol]
public class StreamTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    public override string Me([CallerFilePath] string? filePath = null, [CallerMemberName] string? caller = null) =>
        base.Me(filePath, caller) + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    [Fact]
    public async Task is_stream_type()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();
        db.StreamAdd(key, "field1", "value1");

        //Act
        var keyType = db.KeyType(key);

        //Assert
        keyType.Should().Be(RedisType.Stream);
    }

    [Fact]
    public async Task stream_add_single_pair_with_auto_id()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();

        //Act
        var messageId = db.StreamAdd(key, "field1", "value1");

        //Assert
        (messageId != RedisValue.Null && ((string?)messageId)?.Length > 0).Should().BeTrue();
    }

    [Fact]
    public async Task stream_add_multiple_value_pairs_with_auto_id()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();
        var fields = new[]
        {
            new NameValueEntry("field1", "value1"),
            new NameValueEntry("field2", "value2"),
        };

        var messageId = db.StreamAdd(key, fields);

        var entries = db.StreamRange(key);

        entries.Should().ContainSingle();
        entries[0].Id.Should().Be(messageId);
        var vals = entries[0].Values;
        vals.Should().NotBeNull();
        vals.Length.Should().Be(2);
        vals[0].Name.Should().Be("field1");
        vals[0].Value.Should().Be("value1");
        vals[1].Name.Should().Be("field2");
        vals[1].Value.Should().Be("value2");
    }

    [Fact]
    public async Task stream_add_with_manual_id()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        const string id = "42-0";
        var key = Me();

        //Act
        var messageId = db.StreamAdd(key, "field1", "value1", id);

        //Assert
        messageId.Should().Be(id);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task stream_add_create_stream_false(bool pairs, bool useAsync)
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);
        var db = conn.GetDatabase();
        var key = Me() + $":{pairs}:{useAsync}";
        await db.KeyDeleteAsync(key);

        async Task<RedisValue> Add(bool createStream)
        {
            var options = new StreamAddOptions { CreateStream = createStream };
            if (pairs)
            {
                NameValueEntry[] fields = [new("field1", "value1"), new("field2", "value2")];
                return useAsync
                    ? await db.StreamAddAsync(key, fields, options)
                    : db.StreamAdd(key, fields, options);
            }

            return useAsync
                ? await db.StreamAddAsync(key, "field", "value", options)
                : db.StreamAdd(key, "field", "value", options);
        }

        // no stream, and we declined to create one: nothing happens, and we are told so
        (await Add(createStream: false)).Should().Be(RedisValue.Null);
        (await db.KeyExistsAsync(key)).Should().BeFalse();

        // ...but once the stream exists, the same call appends as normal
        (await Add(createStream: true)).Should().NotBe(RedisValue.Null);
        (await Add(createStream: false)).Should().NotBe(RedisValue.Null);
        (await db.StreamLengthAsync(key)).Should().Be(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task stream_add_trims_by_min_id(bool approximate)
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);
        var db = conn.GetDatabase();
        var key = Me() + $":{approximate}";
        await db.KeyDeleteAsync(key);

        for (var i = 1; i <= 5; i++)
        {
            db.StreamAdd(key, "f", i, new StreamAddOptions { MessageId = $"{i}-1" });
        }
        (await db.StreamLengthAsync(key)).Should().Be(5);

        // exact MINID must drop everything below the threshold; the approximate form is
        // free to keep more, so only assert what the server guarantees in each mode
        db.StreamAdd(key, "f", 6, new StreamAddOptions { MessageId = "6-1", MinId = "4-1", Approximate = approximate });

        var entries = await db.StreamRangeAsync(key);
        entries.Should().Contain(x => x.Id == "6-1");
        if (approximate)
        {
            (entries.Length <= 6).Should().BeTrue();
        }
        else
        {
            entries.Length.Should().Be(3); // 4-1, 5-1, 6-1
            entries.Should().NotContain(x => x.Id == "3-1");
        }
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task stream_add_idempotent_id(bool iid, bool pairs, bool async)
    {
        await using var conn = Create(require: RedisFeatures.v8_6_0);
        var db = conn.GetDatabase();
        StreamIdempotentId id = iid ? new StreamIdempotentId("pid", "iid") : new StreamIdempotentId("pid");
        Log($"id: {id}");
        var key = Me();
        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        async Task<RedisValue> Add()
        {
            if (pairs)
            {
                NameValueEntry[] fields = [new("field1", "value1"), new("field2", "value2"), new("field3", "value3")];
                if (async)
                {
                    return await db.StreamAddAsync(key, fields, idempotentId: id);
                }

                return db.StreamAdd(key, fields, idempotentId: id);
            }

            if (async)
            {
                return await db.StreamAddAsync(key, "field1", "value1", idempotentId: id);
            }

            return db.StreamAdd(key, "field1", "value1", idempotentId: id);
        }

        RedisValue first = await Add();
        Log($"Message ID: {first}");

        RedisValue second = await Add();
        second.Should().Be(first); // idempotent id has avoided a duplicate
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData(null, 42, false)]
    [InlineData(13, null, false)]
    [InlineData(13, 42, false)]
    [InlineData(null, null, true)]
    [InlineData(null, 42, true)]
    [InlineData(13, null, true)]
    [InlineData(13, 42, true)]
    public async Task stream_configure(int? duration, int? maxsize, bool async)
    {
        await using var conn = Create(require: RedisFeatures.v8_6_0);
        var db = conn.GetDatabase();

        var key = Me();
        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);
        var id = await db.StreamAddAsync(key, "field1", "value1");
        Log($"id: {id}");
        var settings = new StreamConfiguration { IdmpDuration = duration, IdmpMaxSize = maxsize };
        bool doomed = duration is null && maxsize is null;
        if (async)
        {
            if (doomed)
            {
                var ex = await Assert.ThrowsAsync<RedisServerException>(async () => await db.StreamConfigureAsync(key, settings));
                ex.Message.Should().StartWith("ERR At least one parameter must be specified");
            }
            else
            {
                await db.StreamConfigureAsync(key, settings);
            }
        }
        else
        {
            if (doomed)
            {
                var ex = Assert.Throws<RedisServerException>(() => db.StreamConfigure(key, settings));
                ex.Message.Should().StartWith("ERR At least one parameter must be specified");
            }
            else
            {
                db.StreamConfigure(key, settings);
            }
        }
        var info = async ? await db.StreamInfoAsync(key) : db.StreamInfo(key);
        const int SERVER_DEFAULT = 100;
        info.IdmpDuration.Should().Be(duration ?? SERVER_DEFAULT);
        info.IdmpMaxSize.Should().Be(maxsize ?? SERVER_DEFAULT);
    }

    [Fact]
    public async Task stream_add_multiple_value_pairs_with_manual_id()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        const string id = "42-0";
        var key = Me();

        var fields = new[]
        {
            new NameValueEntry("field1", "value1"),
            new NameValueEntry("field2", "value2"),
        };

        var messageId = db.StreamAdd(key, fields, id);
        var entries = db.StreamRange(key);

        messageId.Should().Be(id);
        entries.Should().NotBeNull();
        entries.Should().ContainSingle();
        entries[0].Id.Should().Be(id);
    }

    [Fact]
    public async Task stream_auto_claim_missing_key()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var key = Me();
        var db = conn.GetDatabase();
        const string group = "consumerGroup",
                     consumer = "consumer";

        //Act
        db.KeyDelete(key);

        //Assert
        var ex = Assert.Throws<RedisServerException>(() => db.StreamAutoClaim(key, group, consumer, 0, "0-0"));
        ex.Message.Should().StartWith("NOGROUP No such key");

        ex = await Assert.ThrowsAsync<RedisServerException>(() => db.StreamAutoClaimAsync(key, group, consumer, 0, "0-0"));
        ex.Message.Should().StartWith("NOGROUP No such key");
    }

    [Fact]
    public async Task stream_auto_claim_claims_pending_messages()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var key = Me();
        var db = conn.GetDatabase();
        const string group = "consumerGroup",
                     consumer1 = "c1",
                     consumer2 = "c2";

        // Create Consumer Group, add messages, and read messages into a consumer.
        _ = StreamAutoClaim_PrepareTestData(db, key, group, consumer1);

        // Claim any pending messages and reassign them to consumer2.
        var result = db.StreamAutoClaim(key, group, consumer2, 0, "0-0");

        result.NextStartId.Should().Be("0-0");
        result.ClaimedEntries.Should().NotBeEmpty();
        result.DeletedIds.Should().BeEmpty();
        result.ClaimedEntries.Length.Should().Be(2);
        result.ClaimedEntries[0].Values[0].Value.Should().Be("value1");
        result.ClaimedEntries[1].Values[0].Value.Should().Be("value2");
    }

    [Fact]
    public async Task stream_auto_claim_claims_pending_messages_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var key = Me();
        var db = conn.GetDatabase();
        const string group = "consumerGroup",
                     consumer1 = "c1",
                     consumer2 = "c2";

        // Create Consumer Group, add messages, and read messages into a consumer.
        _ = StreamAutoClaim_PrepareTestData(db, key, group, consumer1);

        // Claim any pending messages and reassign them to consumer2.
        var result = await db.StreamAutoClaimAsync(key, group, consumer2, 0, "0-0");

        result.NextStartId.Should().Be("0-0");
        result.ClaimedEntries.Should().NotBeEmpty();
        result.DeletedIds.Should().BeEmpty();
        result.ClaimedEntries.Length.Should().Be(2);
        result.ClaimedEntries[0].Values[0].Value.Should().Be("value1");
        result.ClaimedEntries[1].Values[0].Value.Should().Be("value2");
    }

    [Fact]
    public async Task stream_auto_claim_claims_single_message_with_count_option()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var key = Me();
        var db = conn.GetDatabase();
        const string group = "consumerGroup",
                     consumer1 = "c1",
                     consumer2 = "c2";

        // Create Consumer Group, add messages, and read messages into a consumer.
        var messageIds = StreamAutoClaim_PrepareTestData(db, key, group, consumer1);

        // Claim a single pending message and reassign it to consumer2.
        var result = db.StreamAutoClaim(key, group, consumer2, 0, "0-0", count: 1);

        // Should be the second message ID from the call to prepare.
        result.NextStartId.Should().Be(messageIds[1]);
        result.ClaimedEntries.Should().NotBeEmpty();
        result.DeletedIds.Should().BeEmpty();
        result.ClaimedEntries.Should().ContainSingle();
        result.ClaimedEntries[0].Values[0].Value.Should().Be("value1");
    }

    [Fact]
    public async Task stream_auto_claim_claims_single_message_with_count_option_ids_only()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var key = Me();
        var db = conn.GetDatabase();
        const string group = "consumerGroup",
                     consumer1 = "c1",
                     consumer2 = "c2";

        // Create Consumer Group, add messages, and read messages into a consumer.
        var messageIds = StreamAutoClaim_PrepareTestData(db, key, group, consumer1);

        // Claim a single pending message and reassign it to consumer2.
        var result = db.StreamAutoClaimIdsOnly(key, group, consumer2, 0, "0-0", count: 1);

        // Should be the second message ID from the call to prepare.
        result.NextStartId.Should().Be(messageIds[1]);
        result.ClaimedIds.Should().NotBeEmpty();
        result.ClaimedIds.Should().ContainSingle();
        result.ClaimedIds[0].Should().Be(messageIds[0]);
        result.DeletedIds.Should().BeEmpty();
    }

    [Fact]
    public async Task stream_auto_claim_claims_single_message_with_count_option_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var key = Me();
        var db = conn.GetDatabase();
        const string group = "consumerGroup",
                     consumer1 = "c1",
                     consumer2 = "c2";

        // Create Consumer Group, add messages, and read messages into a consumer.
        var messageIds = StreamAutoClaim_PrepareTestData(db, key, group, consumer1);

        // Claim a single pending message and reassign it to consumer2.
        var result = await db.StreamAutoClaimAsync(key, group, consumer2, 0, "0-0", count: 1);

        // Should be the second message ID from the call to prepare.
        result.NextStartId.Should().Be(messageIds[1]);
        result.ClaimedEntries.Should().NotBeEmpty();
        result.DeletedIds.Should().BeEmpty();
        result.ClaimedEntries.Should().ContainSingle();
        result.ClaimedEntries[0].Values[0].Value.Should().Be("value1");
    }

    [Fact]
    public async Task stream_auto_claim_claims_single_message_with_count_option_ids_only_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var key = Me();
        var db = conn.GetDatabase();
        const string group = "consumerGroup",
                     consumer1 = "c1",
                     consumer2 = "c2";

        // Create Consumer Group, add messages, and read messages into a consumer.
        var messageIds = StreamAutoClaim_PrepareTestData(db, key, group, consumer1);

        // Claim a single pending message and reassign it to consumer2.
        var result = await db.StreamAutoClaimIdsOnlyAsync(key, group, consumer2, 0, "0-0", count: 1);

        // Should be the second message ID from the call to prepare.
        result.NextStartId.Should().Be(messageIds[1]);
        result.ClaimedIds.Should().NotBeEmpty();
        result.ClaimedIds.Should().ContainSingle();
        result.ClaimedIds[0].Should().Be(messageIds[0]);
        result.DeletedIds.Should().BeEmpty();
    }

    [Fact]
    public async Task stream_auto_claim_includes_deleted_message_id()
    {
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var key = Me();
        var db = conn.GetDatabase();
        const string group = "consumerGroup",
                     consumer1 = "c1",
                     consumer2 = "c2";

        // Create Consumer Group, add messages, and read messages into a consumer.
        var messageIds = StreamAutoClaim_PrepareTestData(db, key, group, consumer1);

        // Delete one of the messages, it should be included in the deleted message ID array.
        db.StreamDelete(key, [messageIds[0]]);

        // Claim a single pending message and reassign it to consumer2.
        var result = db.StreamAutoClaim(key, group, consumer2, 0, "0-0", count: 2);

        result.NextStartId.Should().Be("0-0");
        result.ClaimedEntries.Should().NotBeEmpty();
        result.DeletedIds.Should().NotBeEmpty();
        result.ClaimedEntries.Should().ContainSingle();
        result.DeletedIds.Should().ContainSingle();
        result.DeletedIds[0].Should().Be(messageIds[0]);
    }

    [Fact]
    public async Task stream_auto_claim_includes_deleted_message_id_async()
    {
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var key = Me();
        var db = conn.GetDatabase();
        const string group = "consumerGroup",
                     consumer1 = "c1",
                     consumer2 = "c2";

        // Create Consumer Group, add messages, and read messages into a consumer.
        var messageIds = StreamAutoClaim_PrepareTestData(db, key, group, consumer1);

        // Delete one of the messages, it should be included in the deleted message ID array.
        db.StreamDelete(key, [messageIds[0]]);

        // Claim a single pending message and reassign it to consumer2.
        var result = await db.StreamAutoClaimAsync(key, group, consumer2, 0, "0-0", count: 2);

        result.NextStartId.Should().Be("0-0");
        result.ClaimedEntries.Should().NotBeEmpty();
        result.DeletedIds.Should().NotBeEmpty();
        result.ClaimedEntries.Should().ContainSingle();
        result.DeletedIds.Should().ContainSingle();
        result.DeletedIds[0].Should().Be(messageIds[0]);
    }

    [Fact]
    public async Task stream_auto_claim_no_messages_to_claim()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var key = Me();
        var db = conn.GetDatabase();
        const string group = "consumerGroup";

        // Create the group.
        db.KeyDelete(key);
        db.StreamCreateConsumerGroup(key, group, createStream: true);

        // **Don't add any messages to the stream**

        // Claim any pending messages (there aren't any) and reassign them to consumer2.
        var result = db.StreamAutoClaim(key, group, "consumer1", 0, "0-0");

        // Claimed entries should be empty
        result.NextStartId.Should().Be("0-0");
        result.ClaimedEntries.Should().BeEmpty();
        result.DeletedIds.Should().BeEmpty();
    }

    [Fact]
    public async Task stream_auto_claim_no_messages_to_claim_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var key = Me();
        var db = conn.GetDatabase();
        const string group = "consumerGroup";

        // Create the group.
        db.KeyDelete(key);
        db.StreamCreateConsumerGroup(key, group, createStream: true);

        // **Don't add any messages to the stream**

        // Claim any pending messages (there aren't any) and reassign them to consumer2.
        var result = await db.StreamAutoClaimAsync(key, group, "consumer1", 0, "0-0");

        // Claimed entries should be empty
        result.NextStartId.Should().Be("0-0");
        result.ClaimedEntries.Should().BeEmpty();
        result.DeletedIds.Should().BeEmpty();
    }

    [Fact]
    public async Task stream_auto_claim_no_message_meets_min_idle_time()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var key = Me();
        var db = conn.GetDatabase();
        const string group = "consumerGroup",
                     consumer1 = "c1",
                     consumer2 = "c2";

        // Create Consumer Group, add messages, and read messages into a consumer.
        _ = StreamAutoClaim_PrepareTestData(db, key, group, consumer1);

        // Claim messages idle for more than 5 minutes, should return an empty array.
        var result = db.StreamAutoClaim(key, group, consumer2, 300000, "0-0");

        result.NextStartId.Should().Be("0-0");
        result.ClaimedEntries.Should().BeEmpty();
        result.DeletedIds.Should().BeEmpty();
    }

    [Fact]
    public async Task stream_auto_claim_no_message_meets_min_idle_time_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var key = Me();
        var db = conn.GetDatabase();
        const string group = "consumerGroup",
                     consumer1 = "c1",
                     consumer2 = "c2";

        // Create Consumer Group, add messages, and read messages into a consumer.
        _ = StreamAutoClaim_PrepareTestData(db, key, group, consumer1);

        // Claim messages idle for more than 5 minutes, should return an empty array.
        var result = await db.StreamAutoClaimAsync(key, group, consumer2, 300000, "0-0");

        result.NextStartId.Should().Be("0-0");
        result.ClaimedEntries.Should().BeEmpty();
        result.DeletedIds.Should().BeEmpty();
    }

    [Fact]
    public async Task stream_auto_claim_returns_message_id_only()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var key = Me();
        var db = conn.GetDatabase();
        const string group = "consumerGroup",
                     consumer1 = "c1",
                     consumer2 = "c2";

        // Create Consumer Group, add messages, and read messages into a consumer.
        var messageIds = StreamAutoClaim_PrepareTestData(db, key, group, consumer1);

        // Claim any pending messages and reassign them to consumer2.
        var result = db.StreamAutoClaimIdsOnly(key, group, consumer2, 0, "0-0");

        result.NextStartId.Should().Be("0-0");
        result.ClaimedIds.Should().NotBeEmpty();
        result.DeletedIds.Should().BeEmpty();
        result.ClaimedIds.Length.Should().Be(2);
        result.ClaimedIds[0].Should().Be(messageIds[0]);
        result.ClaimedIds[1].Should().Be(messageIds[1]);
    }

    [Fact]
    public async Task stream_auto_claim_returns_message_id_only_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var key = Me();
        var db = conn.GetDatabase();
        const string group = "consumerGroup",
                     consumer1 = "c1",
                     consumer2 = "c2";

        // Create Consumer Group, add messages, and read messages into a consumer.
        var messageIds = StreamAutoClaim_PrepareTestData(db, key, group, consumer1);

        // Claim any pending messages and reassign them to consumer2.
        var result = await db.StreamAutoClaimIdsOnlyAsync(key, group, consumer2, 0, "0-0");

        result.NextStartId.Should().Be("0-0");
        result.ClaimedIds.Should().NotBeEmpty();
        result.DeletedIds.Should().BeEmpty();
        result.ClaimedIds.Length.Should().Be(2);
        result.ClaimedIds[0].Should().Be(messageIds[0]);
        result.ClaimedIds[1].Should().Be(messageIds[1]);
    }

    private static RedisValue[] StreamAutoClaim_PrepareTestData(IDatabase db, RedisKey key, RedisValue group, RedisValue consumer)
    {
        // Create the group.
        db.KeyDelete(key);
        db.StreamCreateConsumerGroup(key, group, createStream: true);

        // Add some messages
        var id1 = db.StreamAdd(key, "field1", "value1");
        var id2 = db.StreamAdd(key, "field2", "value2");

        // Read the messages into the "c1"
        db.StreamReadGroup(key, group, consumer);

        return [id1, id2];
    }

    [Fact]
    public async Task stream_consumer_group_set_id()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();
        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);
        const string groupName = "test_group", consumer = "consumer";

        // Create a stream
        db.StreamAdd(key, "field1", "value1");
        db.StreamAdd(key, "field2", "value2");

        // Create a group and set the position to deliver new messages only.
        db.StreamCreateConsumerGroup(key, groupName, StreamPosition.NewMessages);

        // Read into the group, expect nothing
        var firstRead = db.StreamReadGroup(key, groupName, consumer, StreamPosition.NewMessages);

        // Reset the ID back to read from the beginning.
        db.StreamConsumerGroupSetPosition(key, groupName, StreamPosition.Beginning);

        var secondRead = db.StreamReadGroup(key, groupName, consumer, StreamPosition.NewMessages);

        firstRead.Should().NotBeNull();
        secondRead.Should().NotBeNull();
        firstRead.Should().BeEmpty();
        secondRead.Length.Should().Be(2);
    }

    [Fact]
    public async Task stream_consumer_group_auto_claim_multi_stream()
    {
        await using var conn = Create(require: RedisFeatures.v8_4_0_rc1);

        var db = conn.GetDatabase();
        var key = Me();
        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);
        const string groupName = "test_group", consumer = "consumer";

        // Create a group and set the position to deliver new messages only.
        await db.StreamCreateConsumerGroupAsync(key, groupName, StreamPosition.NewMessages);

        // add some entries
        await db.StreamAddAsync(key, "field1", "value1");
        await db.StreamAddAsync(key, "field2", "value2");

        var idleTime = TimeSpan.FromMilliseconds(100);
        // Read into the group, expect the two entries; we don't expect any data
        // here, at least on a fast server, because it hasn't been idle long enough.
        StreamPosition[] positions = [new(key, StreamPosition.NewMessages)];
        var groups = await db.StreamReadGroupAsync(positions, groupName, consumer, noAck: false, countPerStream: 10, claimMinIdleTime: idleTime);
        var grp = Assert.Single(groups);
        grp.Key.Should().Be(key);
        grp.Entries.Length.Should().Be(2);
        foreach (var entry in grp.Entries)
        {
            entry.DeliveryCount.Should().Be(0); // never delivered before
            entry.IdleTime.Should().Be(TimeSpan.Zero); // never delivered before
        }

        // now repeat immediately; we didn't "ack", so they're still pending, but not idle long enough
        groups = await db.StreamReadGroupAsync(positions, groupName, consumer, noAck: false, countPerStream: 10, claimMinIdleTime: idleTime);
        groups.Should().BeEmpty(); // nothing available from any group

        // wait long enough for the messages to be considered idle
        await Task.Delay(idleTime + idleTime, TestContext.Current.CancellationToken);

        // repeat again; we should get the entries
        groups = await db.StreamReadGroupAsync(positions, groupName, consumer, noAck: false, countPerStream: 10, claimMinIdleTime: idleTime);
        grp = Assert.Single(groups);
        grp.Key.Should().Be(key);
        grp.Entries.Length.Should().Be(2);
        foreach (var entry in grp.Entries)
        {
            entry.DeliveryCount.Should().Be(1); // this is a redelivery
            (entry.IdleTime > TimeSpan.Zero).Should().BeTrue(); // and is considered idle
        }
    }

    [Fact]
    public async Task stream_consumer_group_auto_claim_single_stream()
    {
        await using var conn = Create(require: RedisFeatures.v8_4_0_rc1);

        var db = conn.GetDatabase();
        var key = Me();
        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);
        const string groupName = "test_group", consumer = "consumer";

        // Create a group and set the position to deliver new messages only.
        await db.StreamCreateConsumerGroupAsync(key, groupName, StreamPosition.NewMessages);

        // add some entries
        await db.StreamAddAsync(key, "field1", "value1");
        await db.StreamAddAsync(key, "field2", "value2");

        var idleTime = TimeSpan.FromMilliseconds(100);
        // Read into the group, expect the two entries; we don't expect any data
        // here, at least on a fast server, because it hasn't been idle long enough.
        var entries = await db.StreamReadGroupAsync(key, groupName, consumer, noAck: false, count: 10, claimMinIdleTime: idleTime);
        entries.Length.Should().Be(2);
        foreach (var entry in entries)
        {
            entry.DeliveryCount.Should().Be(0); // never delivered before
            entry.IdleTime.Should().Be(TimeSpan.Zero); // never delivered before
        }

        // now repeat immediately; we didn't "ack", so they're still pending, but not idle long enough
        entries = await db.StreamReadGroupAsync(key, groupName, consumer, null, noAck: false, count: 10, claimMinIdleTime: idleTime);
        entries.Should().BeEmpty(); // nothing available from any group

        // wait long enough for the messages to be considered idle
        await Task.Delay(idleTime + idleTime, TestContext.Current.CancellationToken);

        // repeat again; we should get the entries
        entries = await db.StreamReadGroupAsync(key, groupName, consumer, null, noAck: false, count: 10, claimMinIdleTime: idleTime);
        entries.Length.Should().Be(2);
        foreach (var entry in entries)
        {
            entry.DeliveryCount.Should().Be(1); // this is a redelivery
            (entry.IdleTime > TimeSpan.Zero).Should().BeTrue(); // and is considered idle
        }
    }

    [Fact]
    public async Task stream_consumer_group_with_no_consumers()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string groupName = "test_group";

        // Create a stream
        db.StreamAdd(key, "field1", "value1");

        // Create a group
        db.StreamCreateConsumerGroup(key, groupName, "0-0");

        // Query redis for the group consumers, expect an empty list in response.
        var consumers = db.StreamConsumerInfo(key, groupName);

        consumers.Should().BeEmpty();
    }

    [Fact]
    public async Task stream_create_consumer_group()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string groupName = "test_group";

        // Create a stream
        db.StreamAdd(key, "field1", "value1");

        // Create a group
        var result = db.StreamCreateConsumerGroup(key, groupName, StreamPosition.Beginning);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task stream_create_consumer_group_before_creating_stream()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();

        // Ensure the key doesn't exist.
        var keyExistsBeforeCreate = db.KeyExists(key);

        // The 'createStream' parameter is 'true' by default.
        var groupCreated = db.StreamCreateConsumerGroup(key, "consumerGroup", StreamPosition.NewMessages);

        var keyExistsAfterCreate = db.KeyExists(key);

        keyExistsBeforeCreate.Should().BeFalse();
        groupCreated.Should().BeTrue();
        keyExistsAfterCreate.Should().BeTrue();
    }

    [Fact]
    public async Task stream_create_consumer_group_fails_if_key_doesnt_exist()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();

        // Pass 'false' for 'createStream' to ensure that an
        // exception is thrown when the stream doesn't exist.
        Assert.ThrowsAny<RedisServerException>(() => db.StreamCreateConsumerGroup(
                key,
                "consumerGroup",
                StreamPosition.NewMessages,
                createStream: false));
    }

    [Fact]
    public async Task stream_create_consumer_group_succeeds_when_key_exists()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();

        db.StreamAdd(key, "f1", "v1");

        // Pass 'false' for 'createStream', should create the consumer group
        // without issue since the stream already exists.
        var groupCreated = db.StreamCreateConsumerGroup(
            key,
            "consumerGroup",
            StreamPosition.NewMessages,
            createStream: false);

        groupCreated.Should().BeTrue();
    }

    [Fact]
    public async Task stream_consumer_group_read_only_new_messages_with_empty_response()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string groupName = "test_group";

        // Create a stream
        db.StreamAdd(key, "field1", "value1");
        db.StreamAdd(key, "field2", "value2");

        // Create a group.
        db.StreamCreateConsumerGroup(key, groupName);

        // Read, expect no messages
        var entries = db.StreamReadGroup(key, groupName, "test_consumer", "0-0");

        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task stream_consumer_group_read_from_stream_beginning()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string groupName = "test_group";

        var id1 = db.StreamAdd(key, "field1", "value1");
        var id2 = db.StreamAdd(key, "field2", "value2");

        db.StreamCreateConsumerGroup(key, groupName, StreamPosition.Beginning);

        //Act
        var entries = db.StreamReadGroup(key, groupName, "test_consumer", StreamPosition.NewMessages);

        //Assert
        entries.Length.Should().Be(2);
        (id1 == entries[0].Id).Should().BeTrue();
        (id2 == entries[1].Id).Should().BeTrue();
    }

    [Fact]
    public async Task stream_consumer_group_read_from_stream_beginning_with_count()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string groupName = "test_group";

        var id1 = db.StreamAdd(key, "field1", "value1");
        var id2 = db.StreamAdd(key, "field2", "value2");
        var id3 = db.StreamAdd(key, "field3", "value3");
        _ = db.StreamAdd(key, "field4", "value4");

        // Start reading after id1.
        db.StreamCreateConsumerGroup(key, groupName, id1);

        var entries = db.StreamReadGroup(key, groupName, "test_consumer", StreamPosition.NewMessages, 2);

        // Ensure we only received the requested count and that the IDs match the expected values.
        entries.Length.Should().Be(2);
        (id2 == entries[0].Id).Should().BeTrue();
        (id3 == entries[1].Id).Should().BeTrue();
    }

    [Fact]
    public async Task stream_consumer_group_acknowledge_message()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string groupName = "test_group",
                     consumer = "test_consumer";

        var id1 = db.StreamAdd(key, "field1", "value1");
        var id2 = db.StreamAdd(key, "field2", "value2");
        var id3 = db.StreamAdd(key, "field3", "value3");
        RedisValue notexist = "0-0";
        var id4 = db.StreamAdd(key, "field4", "value4");

        db.StreamCreateConsumerGroup(key, groupName, StreamPosition.Beginning);

        // Read all 4 messages, they will be assigned to the consumer
        var entries = db.StreamReadGroup(key, groupName, consumer, StreamPosition.NewMessages);
        entries.Length.Should().Be(4);

        // Send XACK for 3 of the messages

        // Single message Id overload.
        var oneAck = db.StreamAcknowledge(key, groupName, id1);
        oneAck.Should().Be(1);

        var nack = db.StreamAcknowledge(key, groupName, notexist);
        nack.Should().Be(0);

        // Multiple message Id overload.
        var twoAck = db.StreamAcknowledge(key, groupName, [id3, notexist, id4]);

        // Read the group again, it should only return the unacknowledged message.
        var notAcknowledged = db.StreamReadGroup(key, groupName, consumer, "0-0");

        twoAck.Should().Be(2);
        notAcknowledged.Should().ContainSingle();
        notAcknowledged[0].Id.Should().Be(id2);
    }

    [Theory]
    [InlineData(StreamTrimMode.KeepReferences)]
    [InlineData(StreamTrimMode.DeleteReferences)]
    [InlineData(StreamTrimMode.Acknowledged)]
    public void stream_consumer_group_acknowledge_and_delete_message(StreamTrimMode mode)
    {
        using var conn = Create(require: RedisFeatures.v8_2_0_rc1);

        var db = conn.GetDatabase();
        var key = Me() + ":" + mode;
        const string groupName = "test_group",
            consumer = "test_consumer";

        var id1 = db.StreamAdd(key, "field1", "value1");
        var id2 = db.StreamAdd(key, "field2", "value2");
        var id3 = db.StreamAdd(key, "field3", "value3");
        RedisValue notexist = "0-0";
        var id4 = db.StreamAdd(key, "field4", "value4");

        db.StreamCreateConsumerGroup(key, groupName, StreamPosition.Beginning);

        // Read all 4 messages, they will be assigned to the consumer
        var entries = db.StreamReadGroup(key, groupName, consumer, StreamPosition.NewMessages);
        entries.Length.Should().Be(4);

        // Send XACK for 3 of the messages

        // Single message Id overload.
        var oneAck = db.StreamAcknowledgeAndDelete(key, groupName, mode, id1);
        oneAck.Should().Be(StreamTrimResult.Deleted);

        StreamTrimResult nack = db.StreamAcknowledgeAndDelete(key, groupName, mode, notexist);
        nack.Should().Be(StreamTrimResult.NotFound);

        // Multiple message Id overload.
        RedisValue[] ids = new[] { id3, notexist, id4 };
        var twoAck = db.StreamAcknowledgeAndDelete(key, groupName, mode, ids);

        // Read the group again, it should only return the unacknowledged message.
        var notAcknowledged = db.StreamReadGroup(key, groupName, consumer, "0-0");

        twoAck.Length.Should().Be(3);
        twoAck[0].Should().Be(StreamTrimResult.Deleted);
        twoAck[1].Should().Be(StreamTrimResult.NotFound);
        twoAck[2].Should().Be(StreamTrimResult.Deleted);

        notAcknowledged.Should().ContainSingle();
        notAcknowledged[0].Id.Should().Be(id2);
    }

    [Theory]
    [InlineData(StreamNackMode.Silent, false)]
    [InlineData(StreamNackMode.Silent, true)]
    [InlineData(StreamNackMode.Fail, false)]
    [InlineData(StreamNackMode.Fail, true)]
    [InlineData(StreamNackMode.Fatal, false)]
    [InlineData(StreamNackMode.Fatal, true)]
    public async Task stream_consumer_group_negative_acknowledge_message(StreamNackMode mode, bool async)
    {
        await using var conn = Create(require: RedisFeatures.v8_8_0);

        var db = conn.GetDatabase();
        var key = Me() + ":" + mode + ":" + async;
        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);
        const string groupName = "test_group",
                     consumer = "test_consumer";

        var id1 = db.StreamAdd(key, "field1", "value1");
        var id2 = db.StreamAdd(key, "field2", "value2");
        var id3 = db.StreamAdd(key, "field3", "value3");
        RedisValue notexist = "0-0";

        db.StreamCreateConsumerGroup(key, groupName, StreamPosition.Beginning, flags: CommandFlags.FireAndForget);

        var entries = db.StreamReadGroup(key, groupName, consumer, StreamPosition.NewMessages);
        entries.Length.Should().Be(3);

        long oneNack = async
            ? await db.StreamNegativeAcknowledgeAsync(key, groupName, mode, id1)
            : db.StreamNegativeAcknowledge(key, groupName, mode, id1);
        oneNack.Should().Be(1);

        long zeroNack = async
            ? await db.StreamNegativeAcknowledgeAsync(key, groupName, mode, notexist)
            : db.StreamNegativeAcknowledge(key, groupName, mode, notexist);
        zeroNack.Should().Be(0);

        long oneArrayNack = async
            ? await db.StreamNegativeAcknowledgeAsync(key, groupName, mode, [id2])
            : db.StreamNegativeAcknowledge(key, groupName, mode, [id2]);
        oneArrayNack.Should().Be(1);

        long multiArrayNack = async
            ? await db.StreamNegativeAcknowledgeAsync(key, groupName, mode, [id3, notexist])
            : db.StreamNegativeAcknowledge(key, groupName, mode, [id3, notexist]);
        multiArrayNack.Should().Be(1);

        var consumerPending = db.StreamPendingMessages(key, groupName, 10, consumer);
        consumerPending.Should().BeEmpty();

        var allPending = db.StreamPendingMessages(key, groupName, 10, RedisValue.Null);
        allPending.Length.Should().Be(3);
        allPending.Should().Contain(x => x.MessageId == id1 && x.ConsumerName.IsNullOrEmpty);
        allPending.Should().Contain(x => x.MessageId == id2 && x.ConsumerName.IsNullOrEmpty);
        allPending.Should().Contain(x => x.MessageId == id3 && x.ConsumerName.IsNullOrEmpty);
        if (mode == StreamNackMode.Fatal)
        {
            allPending.Should().AllSatisfy(x => x.DeliveryCount.Should().Be(int.MinValue));
        }
    }

    [Fact]
    public async Task stream_consumer_group_claim_messages()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string groupName = "test_group",
                     consumer1 = "test_consumer_1",
                     consumer2 = "test_consumer_2";

        _ = db.StreamAdd(key, "field1", "value1");
        _ = db.StreamAdd(key, "field2", "value2");
        _ = db.StreamAdd(key, "field3", "value3");
        _ = db.StreamAdd(key, "field4", "value4");

        db.StreamCreateConsumerGroup(key, groupName, "0-0");

        // Read a single message into the first consumer.
        db.StreamReadGroup(key, groupName, consumer1, count: 1);

        // Read the remaining messages into the second consumer.
        db.StreamReadGroup(key, groupName, consumer2);

        // Claim the 3 messages consumed by consumer2 for consumer1.

        // Get the pending messages for consumer2.
        var pendingMessages = db.StreamPendingMessages(
            key,
            groupName,
            10,
            consumer2);

        // Claim the messages for consumer1.
        var messages = db.StreamClaim(
                            key,
                            groupName,
                            consumer1,
                            0, // Min message idle time
                            messageIds: pendingMessages.Select(pm => pm.MessageId).ToArray());

        // Now see how many messages are pending for each consumer
        var pendingSummary = db.StreamPending(key, groupName);

        pendingSummary.Consumers.Should().NotBeNull();
        pendingSummary.Consumers.Should().ContainSingle();
        pendingSummary.Consumers[0].PendingMessageCount.Should().Be(4);
        messages.Length.Should().Be(pendingMessages.Length);
    }

    [Fact]
    public async Task stream_consumer_group_claim_messages_returning_ids()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string groupName = "test_group",
                     consumer1 = "test_consumer_1",
                     consumer2 = "test_consumer_2";

        _ = db.StreamAdd(key, "field1", "value1");
        var id2 = db.StreamAdd(key, "field2", "value2");
        var id3 = db.StreamAdd(key, "field3", "value3");
        var id4 = db.StreamAdd(key, "field4", "value4");

        db.StreamCreateConsumerGroup(key, groupName, StreamPosition.Beginning);

        // Read a single message into the first consumer.
        _ = db.StreamReadGroup(key, groupName, consumer1, StreamPosition.NewMessages, 1);

        // Read the remaining messages into the second consumer.
        _ = db.StreamReadGroup(key, groupName, consumer2);

        // Claim the 3 messages consumed by consumer2 for consumer1.

        // Get the pending messages for consumer2.
        var pendingMessages = db.StreamPendingMessages(
            key,
            groupName,
            10,
            consumer2);

        // Claim the messages for consumer1.
        var messageIds = db.StreamClaimIdsOnly(
                            key,
                            groupName,
                            consumer1,
                            0, // Min message idle time
                            messageIds: pendingMessages.Select(pm => pm.MessageId).ToArray());

        // We should get an array of 3 message IDs.
        messageIds.Length.Should().Be(3);
        messageIds[0].Should().Be(id2);
        messageIds[1].Should().Be(id3);
        messageIds[2].Should().Be(id4);
    }

    [Fact]
    public async Task stream_consumer_group_read_multiple_one_read_beginning_one_read_new()
    {
        // Create a group for each stream. One set to read from the beginning of the
        // stream and the other to begin reading only new messages.

        // Ask redis to read from the beginning of both stream, expect messages
        // for only the stream set to read from the beginning.
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        const string groupName = "test_group";
        var stream1 = Me() + "a";
        var stream2 = Me() + "b";

        db.StreamAdd(stream1, "field1-1", "value1-1");
        db.StreamAdd(stream1, "field1-2", "value1-2");

        db.StreamAdd(stream2, "field2-1", "value2-1");
        db.StreamAdd(stream2, "field2-2", "value2-2");
        db.StreamAdd(stream2, "field2-3", "value2-3");

        // stream1 set up to read only new messages.
        db.StreamCreateConsumerGroup(stream1, groupName, StreamPosition.NewMessages);

        // stream2 set up to read from the beginning of the stream
        db.StreamCreateConsumerGroup(stream2, groupName, StreamPosition.Beginning);

        // Read for both streams from the beginning. We shouldn't get anything back for stream1.
        var pairs = new[]
        {
            // StreamPosition.NewMessages will send ">" which indicates "Undelivered" messages.
            new StreamPosition(stream1, StreamPosition.NewMessages),
            new StreamPosition(stream2, StreamPosition.NewMessages),
        };

        var streams = db.StreamReadGroup(pairs, groupName, "test_consumer");

        streams.Should().NotBeNull();
        streams.Should().ContainSingle();
        streams[0].Key.Should().Be(stream2);
        streams[0].Entries.Length.Should().Be(3);
    }

    [Fact]
    public async Task stream_consumer_group_read_multiple_only_new_messages_expect_no_result()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        const string groupName = "test_group";
        var stream1 = Me() + "a";
        var stream2 = Me() + "b";

        db.StreamAdd(stream1, "field1-1", "value1-1");
        db.StreamAdd(stream2, "field2-1", "value2-1");

        // set both streams to read only new messages (default behavior).
        db.StreamCreateConsumerGroup(stream1, groupName);
        db.StreamCreateConsumerGroup(stream2, groupName);

        // We shouldn't get anything for either stream.
        var pairs = new[]
        {
            new StreamPosition(stream1, StreamPosition.Beginning),
            new StreamPosition(stream2, StreamPosition.Beginning),
        };

        var streams = db.StreamReadGroup(pairs, groupName, "test_consumer");

        streams.Should().NotBeNull();
        streams.Length.Should().Be(2);
        streams[0].Entries.Should().BeEmpty();
        streams[1].Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task stream_consumer_group_read_multiple_only_new_messages_expect1_result()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        const string groupName = "test_group";
        var stream1 = Me() + "a";
        var stream2 = Me() + "b";

        // These messages won't be read.
        db.StreamAdd(stream1, "field1-1", "value1-1");
        db.StreamAdd(stream2, "field2-1", "value2-1");

        // set both streams to read only new messages (default behavior).
        db.StreamCreateConsumerGroup(stream1, groupName);
        db.StreamCreateConsumerGroup(stream2, groupName);

        // We should read these though.
        var id1 = db.StreamAdd(stream1, "field1-2", "value1-2");
        var id2 = db.StreamAdd(stream2, "field2-2", "value2-2");

        // Read the new messages (messages created after the group was created).
        var pairs = new[]
        {
            new StreamPosition(stream1, StreamPosition.NewMessages),
            new StreamPosition(stream2, StreamPosition.NewMessages),
        };

        var streams = db.StreamReadGroup(pairs, groupName, "test_consumer");

        streams.Should().NotBeNull();
        streams.Length.Should().Be(2);
        streams[0].Entries.Should().ContainSingle();
        streams[1].Entries.Should().ContainSingle();
        streams[0].Entries[0].Id.Should().Be(id1);
        streams[1].Entries[0].Id.Should().Be(id2);
    }

    [Fact]
    public async Task stream_consumer_group_read_multiple_restrict_count()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        const string groupName = "test_group";
        var stream1 = Me() + "a";
        var stream2 = Me() + "b";

        var id1_1 = db.StreamAdd(stream1, "field1-1", "value1-1");
        var id1_2 = db.StreamAdd(stream1, "field1-2", "value1-2");

        var id2_1 = db.StreamAdd(stream2, "field2-1", "value2-1");
        _ = db.StreamAdd(stream2, "field2-2", "value2-2");
        _ = db.StreamAdd(stream2, "field2-3", "value2-3");

        // Set the initial read point in each stream, *after* the first ID in both streams.
        db.StreamCreateConsumerGroup(stream1, groupName, id1_1);
        db.StreamCreateConsumerGroup(stream2, groupName, id2_1);

        var pairs = new[]
        {
            // Read after the first id in both streams
            new StreamPosition(stream1, StreamPosition.NewMessages),
            new StreamPosition(stream2, StreamPosition.NewMessages),
        };

        // Restrict the count to 2 (expect only 1 message from first stream, 2 from the second).
        var streams = db.StreamReadGroup(pairs, groupName, "test_consumer", 2);

        streams.Should().NotBeNull();
        streams.Length.Should().Be(2);
        streams[0].Entries.Should().ContainSingle();
        streams[1].Entries.Length.Should().Be(2);
        streams[0].Entries[0].Id.Should().Be(id1_2);
    }

    [Fact]
    public async Task stream_read_multiple_max_count()
    {
        await using var conn = Create(require: RedisFeatures.v8_10_0);

        var db = conn.GetDatabase();
        var stream1 = Me() + "a";
        var stream2 = Me() + "b";

        db.StreamAdd(stream1, "f", "v"); // 3 in stream1
        db.StreamAdd(stream1, "f", "v");
        db.StreamAdd(stream1, "f", "v");
        db.StreamAdd(stream2, "f", "v"); // 2 in stream2
        db.StreamAdd(stream2, "f", "v");

        StreamPosition[] pairs =
        [
            new StreamPosition(stream1, StreamPosition.Beginning),
            new StreamPosition(stream2, StreamPosition.Beginning),
        ];

        // Without a global cap, all 5 come back.
        var all = db.StreamRead(pairs, countPerStream: null);
        all.Sum(s => s.Entries.Length).Should().Be(5);

        // MAXCOUNT caps the *total* number of entries across all streams.
        var capped = await db.StreamReadAsync(pairs, countPerStream: null, maxCount: 3);
        capped.Sum(s => s.Entries.Length).Should().Be(3);

        // MAXSIZE still returns at least one entry even with a tiny budget.
        var oneish = db.StreamRead(pairs, countPerStream: null, maxSize: 1);
        (oneish.Sum(s => s.Entries.Length) >= 1).Should().BeTrue();
    }

    [Fact]
    public async Task stream_read_group_multiple_max_count()
    {
        await using var conn = Create(require: RedisFeatures.v8_10_0);

        var db = conn.GetDatabase();
        const string groupName = "test_group";
        var stream1 = Me() + "a";
        var stream2 = Me() + "b";

        db.StreamAdd(stream1, "f", "v");
        db.StreamAdd(stream1, "f", "v");
        db.StreamAdd(stream1, "f", "v");
        db.StreamAdd(stream2, "f", "v");
        db.StreamAdd(stream2, "f", "v");

        db.StreamCreateConsumerGroup(stream1, groupName, StreamPosition.Beginning);
        db.StreamCreateConsumerGroup(stream2, groupName, StreamPosition.Beginning);

        StreamPosition[] pairs =
        [
            new StreamPosition(stream1, StreamPosition.NewMessages),
            new StreamPosition(stream2, StreamPosition.NewMessages),
        ];

        // MAXCOUNT caps the total across all streams (global budget of 3 vs the 5 available).
        var capped = await db.StreamReadGroupAsync(pairs, groupName, "test_consumer", countPerStream: null, noAck: false, claimMinIdleTime: null, maxCount: 3);
        capped.Sum(s => s.Entries.Length).Should().Be(3);
    }

    [Fact]
    public async Task stream_consumer_group_view_pending_info_no_consumers()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string groupName = "test_group";

        db.StreamAdd(key, "field1", "value1");

        db.StreamCreateConsumerGroup(key, groupName, StreamPosition.Beginning);

        //Act
        var pendingInfo = db.StreamPending(key, groupName);

        //Assert
        pendingInfo.PendingMessageCount.Should().Be(0);
        pendingInfo.LowestPendingMessageId.Should().Be(RedisValue.Null);
        pendingInfo.HighestPendingMessageId.Should().Be(RedisValue.Null);
        pendingInfo.Consumers.Should().NotBeNull();
        pendingInfo.Consumers.Should().BeEmpty();
    }

    [Fact]
    public async Task stream_consumer_group_view_pending_info_when_nothing_pending()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string groupName = "test_group";

        db.StreamAdd(key, "field1", "value1");

        db.StreamCreateConsumerGroup(key, groupName, "0-0");

        //Act
        var pendingMessages = db.StreamPendingMessages(
            key,
            groupName,
            10,
            consumerName: RedisValue.Null);

        //Assert
        pendingMessages.Should().NotBeNull();
        pendingMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task stream_consumer_group_view_pending_info_summary()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string groupName = "test_group",
                     consumer1 = "test_consumer_1",
                     consumer2 = "test_consumer_2";

        var id1 = db.StreamAdd(key, "field1", "value1");
        db.StreamAdd(key, "field2", "value2");
        db.StreamAdd(key, "field3", "value3");
        var id4 = db.StreamAdd(key, "field4", "value4");

        db.StreamCreateConsumerGroup(key, groupName, StreamPosition.Beginning);

        // Read a single message into the first consumer.
        db.StreamReadGroup(key, groupName, consumer1, StreamPosition.NewMessages, 1);

        // Read the remaining messages into the second consumer.
        db.StreamReadGroup(key, groupName, consumer2);

        var pendingInfo = db.StreamPending(key, groupName);

        pendingInfo.PendingMessageCount.Should().Be(4);
        pendingInfo.LowestPendingMessageId.Should().Be(id1);
        pendingInfo.HighestPendingMessageId.Should().Be(id4);
        pendingInfo.Consumers.Length.Should().Be(2);

        var consumer1Count = pendingInfo.Consumers.First(c => c.Name == consumer1).PendingMessageCount;
        var consumer2Count = pendingInfo.Consumers.First(c => c.Name == consumer2).PendingMessageCount;

        consumer1Count.Should().Be(1);
        consumer2Count.Should().Be(3);
    }

    [Fact]
    public async Task stream_consumer_group_view_pending_message_info()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string groupName = "test_group",
                     consumer1 = "test_consumer_1",
                     consumer2 = "test_consumer_2";

        var id1 = db.StreamAdd(key, "field1", "value1");
        db.StreamAdd(key, "field2", "value2");
        db.StreamAdd(key, "field3", "value3");
        db.StreamAdd(key, "field4", "value4");

        db.StreamCreateConsumerGroup(key, groupName, StreamPosition.Beginning);

        // Read a single message into the first consumer.
        db.StreamReadGroup(key, groupName, consumer1, count: 1);

        // Read the remaining messages into the second consumer.
        _ = db.StreamReadGroup(key, groupName, consumer2) ?? throw new ArgumentNullException(nameof(consumer2), "db.StreamReadGroup(key, groupName, consumer2)");

        await Task.Delay(10, TestContext.Current.CancellationToken).ForAwait();

        // Get the pending info about the messages themselves.
        var pendingMessageInfoList = db.StreamPendingMessages(key, groupName, 10, RedisValue.Null);

        pendingMessageInfoList.Should().NotBeNull();
        pendingMessageInfoList.Length.Should().Be(4);
        pendingMessageInfoList[0].ConsumerName.Should().Be(consumer1);
        pendingMessageInfoList[0].DeliveryCount.Should().Be(1);
        ((int)pendingMessageInfoList[0].IdleTimeInMilliseconds > 0).Should().BeTrue();
        pendingMessageInfoList[0].MessageId.Should().Be(id1);
    }

    [Fact]
    public async Task stream_consumer_group_view_pending_message_with_min_idle()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string groupName = "test_group",
            consumer1 = "test_consumer_1";
        const int minIdleTimeInMs = 100;

        var id1 = db.StreamAdd(key, "field1", "value1");

        db.StreamCreateConsumerGroup(key, groupName, StreamPosition.Beginning);

        // Read a single message into the first consumer.
        db.StreamReadGroup(key, groupName, consumer1, count: 1);

        var preDelayPendingMessages =
            db.StreamPendingMessages(key, groupName, 10, RedisValue.Null, minId: id1, maxId: id1, minIdleTimeInMs: minIdleTimeInMs);

        await Task.Delay(minIdleTimeInMs * 2, TestContext.Current.CancellationToken).ForAwait();

        var postDelayPendingMessages =
            db.StreamPendingMessages(key, groupName, 10, RedisValue.Null, minId: id1, maxId: id1, minIdleTimeInMs: minIdleTimeInMs);

        preDelayPendingMessages.Should().NotBeNull();
        preDelayPendingMessages.Should().BeEmpty();
        postDelayPendingMessages.Should().NotBeNull();
        postDelayPendingMessages.Should().ContainSingle();
        postDelayPendingMessages[0].DeliveryCount.Should().Be(1);
        ((int)postDelayPendingMessages[0].IdleTimeInMilliseconds > minIdleTimeInMs).Should().BeTrue();
        postDelayPendingMessages[0].MessageId.Should().Be(id1);
    }

    [Fact]
    public async Task stream_consumer_group_view_pending_message_info_for_consumer()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string groupName = "test_group",
                     consumer1 = "test_consumer_1",
                     consumer2 = "test_consumer_2";

        db.StreamAdd(key, "field1", "value1");
        db.StreamAdd(key, "field2", "value2");
        db.StreamAdd(key, "field3", "value3");
        db.StreamAdd(key, "field4", "value4");

        db.StreamCreateConsumerGroup(key, groupName, StreamPosition.Beginning);

        // Read a single message into the first consumer.
        db.StreamReadGroup(key, groupName, consumer1, count: 1);

        // Read the remaining messages into the second consumer.
        db.StreamReadGroup(key, groupName, consumer2);

        // Get the pending info about the messages themselves.
        var pendingMessageInfoList = db.StreamPendingMessages(
            key,
            groupName,
            10,
            consumer2);

        pendingMessageInfoList.Should().NotBeNull();
        pendingMessageInfoList.Length.Should().Be(3);
    }

    [Fact]
    public async Task stream_delete_consumer()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string groupName = "test_group",
                     consumer = "test_consumer";

        // Add a message to create the stream.
        db.StreamAdd(key, "field1", "value1");
        db.StreamAdd(key, "field2", "value2");

        // Create a consumer group and read the message.
        db.StreamCreateConsumerGroup(key, groupName, StreamPosition.Beginning);
        db.StreamReadGroup(key, groupName, consumer, StreamPosition.NewMessages);

        var preDeleteConsumers = db.StreamConsumerInfo(key, groupName);

        // Delete the consumer.
        var deleteResult = db.StreamDeleteConsumer(key, groupName, consumer);

        // Should get 2 messages in the deleteResult.
        var postDeleteConsumers = db.StreamConsumerInfo(key, groupName);

        deleteResult.Should().Be(2);
        preDeleteConsumers.Should().ContainSingle();
        postDeleteConsumers.Should().BeEmpty();
    }

    [Fact]
    public async Task stream_delete_consumer_group()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string groupName = "test_group",
                     consumer = "test_consumer";

        // Add a message to create the stream.
        db.StreamAdd(key, "field1", "value1");

        // Create a consumer group and read the messages.
        db.StreamCreateConsumerGroup(key, groupName, StreamPosition.Beginning);
        db.StreamReadGroup(key, groupName, consumer, StreamPosition.Beginning);

        var preDeleteInfo = db.StreamInfo(key);

        // Now delete the group.
        var deleteResult = db.StreamDeleteConsumerGroup(key, groupName);

        var postDeleteInfo = db.StreamInfo(key);

        deleteResult.Should().BeTrue();
        preDeleteInfo.ConsumerGroupCount.Should().Be(1);
        postDeleteInfo.ConsumerGroupCount.Should().Be(0);
    }

    [Fact]
    public async Task stream_delete_message()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();

        db.StreamAdd(key, "field1", "value1");
        db.StreamAdd(key, "field2", "value2");
        var id3 = db.StreamAdd(key, "field3", "value3");
        db.StreamAdd(key, "field4", "value4");

        var deletedCount = db.StreamDelete(key, [id3]);

        //Act
        var messages = db.StreamRange(key);

        //Assert
        deletedCount.Should().Be(1);
        messages.Length.Should().Be(3);
    }

    [Fact]
    public async Task stream_delete_messages()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();

        db.StreamAdd(key, "field1", "value1");
        var id2 = db.StreamAdd(key, "field2", "value2");
        var id3 = db.StreamAdd(key, "field3", "value3");
        db.StreamAdd(key, "field4", "value4");

        var deletedCount = db.StreamDelete(key, [id2, id3], CommandFlags.None);

        //Act
        var messages = db.StreamRange(key);

        //Assert
        deletedCount.Should().Be(2);
        messages.Length.Should().Be(2);
    }

    [Theory]
    [InlineData(StreamTrimMode.KeepReferences)]
    [InlineData(StreamTrimMode.DeleteReferences)]
    [InlineData(StreamTrimMode.Acknowledged)]
    public void stream_delete_ex_message(StreamTrimMode mode)
    {
        using var conn = Create(require: RedisFeatures.v8_2_0_rc1); // XDELEX

        var db = conn.GetDatabase();
        var key = Me() + ":" + mode;

        db.StreamAdd(key, "field1", "value1");
        db.StreamAdd(key, "field2", "value2");
        var id3 = db.StreamAdd(key, "field3", "value3");
        db.StreamAdd(key, "field4", "value4");

        var deleted = db.StreamDelete(key, new[] { id3 }, mode: mode);
        var messages = db.StreamRange(key);

        Assert.Single(deleted).Should().Be(StreamTrimResult.Deleted);
        messages.Length.Should().Be(3);
    }

    [Theory]
    [InlineData(StreamTrimMode.KeepReferences)]
    [InlineData(StreamTrimMode.DeleteReferences)]
    [InlineData(StreamTrimMode.Acknowledged)]
    public void stream_delete_ex_messages(StreamTrimMode mode)
    {
        using var conn = Create(require: RedisFeatures.v8_2_0_rc1); // XDELEX

        var db = conn.GetDatabase();
        var key = Me() + ":" + mode;

        db.StreamAdd(key, "field1", "value1");
        var id2 = db.StreamAdd(key, "field2", "value2");
        var id3 = db.StreamAdd(key, "field3", "value3");
        db.StreamAdd(key, "field4", "value4");

        var deleted = db.StreamDelete(key, new[] { id2, id3 }, mode: mode);
        var messages = db.StreamRange(key);

        deleted.Length.Should().Be(2);
        deleted[0].Should().Be(StreamTrimResult.Deleted);
        deleted[1].Should().Be(StreamTrimResult.Deleted);
        messages.Length.Should().Be(2);
    }

    [Fact]
    public async Task stream_group_info_get()
    {
        var key = Me();
        const string group1 = "test_group_1",
                     group2 = "test_group_2",
                     consumer1 = "test_consumer_1",
                     consumer2 = "test_consumer_2";

        await using (var conn = Create(require: RedisFeatures.v5_0_0))
        {
            var db = conn.GetDatabase();
            db.KeyDelete(key);

            db.StreamAdd(key, "field1", "value1");
            db.StreamAdd(key, "field2", "value2");
            db.StreamAdd(key, "field3", "value3");
            db.StreamAdd(key, "field4", "value4");

            db.StreamCreateConsumerGroup(key, group1, StreamPosition.Beginning);
            db.StreamCreateConsumerGroup(key, group2, StreamPosition.Beginning);

            var groupInfoList = db.StreamGroupInfo(key);
            groupInfoList[0].EntriesRead.Should().Be(0);
            groupInfoList[0].Lag.Should().Be(4);
            groupInfoList[0].EntriesRead.Should().Be(0);
            groupInfoList[1].Lag.Should().Be(4);

            // Read a single message into the first consumer.
            db.StreamReadGroup(key, group1, consumer1, count: 1);

            // Read the remaining messages into the second consumer.
            db.StreamReadGroup(key, group2, consumer2);

            groupInfoList = db.StreamGroupInfo(key);

            groupInfoList.Should().NotBeNull();
            groupInfoList.Length.Should().Be(2);

            groupInfoList[0].Name.Should().Be(group1);
            groupInfoList[0].PendingMessageCount.Should().Be(1);
            IsMessageId(groupInfoList[0].LastDeliveredId).Should().BeTrue(); // can't test actual - will vary
            groupInfoList[0].EntriesRead.Should().Be(1);
            groupInfoList[0].Lag.Should().Be(3);

            groupInfoList[1].Name.Should().Be(group2);
            groupInfoList[1].PendingMessageCount.Should().Be(4);
            IsMessageId(groupInfoList[1].LastDeliveredId).Should().BeTrue(); // can't test actual - will vary
            groupInfoList[1].EntriesRead.Should().Be(4);
            groupInfoList[1].Lag.Should().Be(0);
        }

        static bool IsMessageId(string? value)
        {
            if (value.IsNullOrWhiteSpace()) return false;
            return value.Length >= 3 && value.Contains('-');
        }
    }

    [Fact]
    public async Task stream_group_consumer_info_get()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string group = "test_group",
                     consumer1 = "test_consumer_1",
                     consumer2 = "test_consumer_2";

        db.StreamAdd(key, "field1", "value1");
        db.StreamAdd(key, "field2", "value2");
        db.StreamAdd(key, "field3", "value3");
        db.StreamAdd(key, "field4", "value4");

        db.StreamCreateConsumerGroup(key, group, StreamPosition.Beginning);
        db.StreamReadGroup(key, group, consumer1, count: 1);
        db.StreamReadGroup(key, group, consumer2);

        //Act
        var consumerInfoList = db.StreamConsumerInfo(key, group);

        //Assert
        consumerInfoList.Should().NotBeNull();
        consumerInfoList.Length.Should().Be(2);

        consumerInfoList[0].Name.Should().Be(consumer1);
        consumerInfoList[1].Name.Should().Be(consumer2);

        consumerInfoList[0].PendingMessageCount.Should().Be(1);
        consumerInfoList[1].PendingMessageCount.Should().Be(3);
    }

    [Fact]
    public async Task stream_info_get()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();

        var id1 = db.StreamAdd(key, "field1", "value1");
        db.StreamAdd(key, "field2", "value2");
        var id3 = db.StreamAdd(key, "field3", "value3");
        db.StreamAdd(key, "field4", "value4");
        var id5 = db.StreamAdd(key, "field5", "value5");
        db.StreamDelete(key, [id3]);
        var streamInfo = db.StreamInfo(key);

        streamInfo.Length.Should().Be(4);
        (streamInfo.RadixTreeKeys > 0).Should().BeTrue();
        (streamInfo.RadixTreeNodes > 0).Should().BeTrue();
        streamInfo.FirstEntry.Id.Should().Be(id1);
        streamInfo.LastEntry.Id.Should().Be(id5);

        var server = conn.GetServer(conn.GetEndPoints().First());
        Log($"server version: {server.Version}");
        if (server.Version.IsAtLeast(RedisFeatures.v7_0_0_rc1))
        {
            streamInfo.MaxDeletedEntryId.Should().Be(id3);
            streamInfo.EntriesAdded.Should().Be(5);
            streamInfo.RecordedFirstEntryId.IsNull.Should().BeFalse();
        }
        else
        {
            streamInfo.MaxDeletedEntryId.IsNull.Should().BeTrue();
            streamInfo.EntriesAdded.Should().Be(-1);
            streamInfo.RecordedFirstEntryId.IsNull.Should().BeTrue();
        }

        if (server.Version.IsAtLeast(RedisFeatures.v8_6_0))
        {
            (streamInfo.IdmpDuration > 0).Should().BeTrue();
            (streamInfo.IdmpMaxSize > 0).Should().BeTrue();
            streamInfo.PidsTracked.Should().Be(0);
            streamInfo.IidsTracked.Should().Be(0);
            streamInfo.IidsDuplicates.Should().Be(0);
            streamInfo.IidsAdded.Should().Be(0);
        }
        else
        {
            streamInfo.IdmpDuration.Should().Be(-1);
            streamInfo.IdmpMaxSize.Should().Be(-1);
            streamInfo.PidsTracked.Should().Be(-1);
            streamInfo.IidsTracked.Should().Be(-1);
            streamInfo.IidsDuplicates.Should().Be(-1);
            streamInfo.IidsAdded.Should().Be(-1);
        }
    }

    [Fact]
    public async Task stream_info_get_with_empty_stream()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();

        // Add an entry and then delete it so the stream is empty, then run streaminfo
        // to ensure it functions properly on an empty stream. Namely, the first-entry
        // and last-entry messages should be null.
        var id = db.StreamAdd(key, "field1", "value1");
        db.StreamDelete(key, [id]);

        db.StreamLength(key).Should().Be(0);

        var streamInfo = db.StreamInfo(key);

        streamInfo.FirstEntry.IsNull.Should().BeTrue();
        streamInfo.LastEntry.IsNull.Should().BeTrue();
    }

    [Fact]
    public async Task stream_no_consumer_groups()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();

        db.StreamAdd(key, "field1", "value1");

        //Act
        var groups = db.StreamGroupInfo(key);

        //Assert
        groups.Should().NotBeNull();
        groups.Should().BeEmpty();
    }

    [Fact]
    public async Task stream_pending_no_messages_or_consumers()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string groupName = "test_group";

        var id = db.StreamAdd(key, "field1", "value1");
        db.StreamDelete(key, [id]);

        db.StreamCreateConsumerGroup(key, groupName, "0-0");

        //Act
        var pendingInfo = db.StreamPending(key, "test_group");

        //Assert
        pendingInfo.PendingMessageCount.Should().Be(0);
        pendingInfo.LowestPendingMessageId.Should().Be(RedisValue.Null);
        pendingInfo.HighestPendingMessageId.Should().Be(RedisValue.Null);
        pendingInfo.Consumers.Should().NotBeNull();
        pendingInfo.Consumers.Should().BeEmpty();
    }

    [Fact]
    public void stream_position_default_value_is_beginning()
    {
        //Act
        RedisValue position = StreamPosition.Beginning;

        //Assert
        StreamPosition.Resolve(position, RedisCommand.XREAD).Should().Be(StreamConstants.AllMessages);
        StreamPosition.Resolve(position, RedisCommand.XREADGROUP).Should().Be(StreamConstants.AllMessages);
        StreamPosition.Resolve(position, RedisCommand.XGROUP).Should().Be(StreamConstants.AllMessages);
    }

    [Fact]
    public void stream_position_validate_beginning()
    {
        //Act
        var position = StreamPosition.Beginning;

        //Assert
        StreamPosition.Resolve(position, RedisCommand.XREAD).Should().Be(StreamConstants.AllMessages);
    }

    [Fact]
    public void stream_position_validate_explicit()
    {
        //Arrange
        const string explicitValue = "1-0";

        //Act
        const string position = explicitValue;

        //Assert
        StreamPosition.Resolve(position, RedisCommand.XREAD).Should().Be(explicitValue);
    }

    [Fact]
    public void stream_position_validate_new()
    {
        //Act
        var position = StreamPosition.NewMessages;

        //Assert
        StreamPosition.Resolve(position, RedisCommand.XGROUP).Should().Be(StreamConstants.NewMessages);
        StreamPosition.Resolve(position, RedisCommand.XREADGROUP).Should().Be(StreamConstants.UndeliveredMessages);
        Assert.ThrowsAny<InvalidOperationException>(() => StreamPosition.Resolve(position, RedisCommand.XREAD));
    }

    [Fact]
    public async Task stream_read()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();

        var id1 = db.StreamAdd(key, "field1", "value1");
        var id2 = db.StreamAdd(key, "field2", "value2");
        var id3 = db.StreamAdd(key, "field3", "value3");

        // Read the entire stream from the beginning.
        var entries = db.StreamRead(key, "0-0");

        entries.Length.Should().Be(3);
        entries[0].Id.Should().Be(id1);
        entries[1].Id.Should().Be(id2);
        entries[2].Id.Should().Be(id3);
    }

    [Fact]
    public async Task stream_read_empty_stream()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();

        // Write to a stream to create the key.
        var id1 = db.StreamAdd(key, "field1", "value1");

        // Delete the key to empty the stream.
        db.StreamDelete(key, [id1]);
        var len = db.StreamLength(key);

        // Read the entire stream from the beginning.
        var entries = db.StreamRead(key, "0-0");

        entries.Should().BeEmpty();
        len.Should().Be(0);
    }

    [Fact]
    public async Task stream_read_empty_streams()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key1 = Me() + "a";
        var key2 = Me() + "b";

        // Write to a stream to create the key.
        var id1 = db.StreamAdd(key1, "field1", "value1");
        var id2 = db.StreamAdd(key2, "field2", "value2");

        // Delete the key to empty the stream.
        db.StreamDelete(key1, [id1]);
        db.StreamDelete(key2, [id2]);

        var len1 = db.StreamLength(key1);
        var len2 = db.StreamLength(key2);

        // Read the entire stream from the beginning.
        var entries1 = db.StreamRead(key1, "0-0");
        var entries2 = db.StreamRead(key2, "0-0");

        entries1.Should().BeEmpty();
        entries2.Should().BeEmpty();

        len1.Should().Be(0);
        len2.Should().Be(0);
    }

    [Fact]
    public async Task stream_read_last_message()
    {
        await using var conn = Create(require: RedisFeatures.v7_4_0_rc1);
        var db = conn.GetDatabase();
        var key1 = Me();

        // Read the entire stream from the beginning.
        db.StreamRead(key1, "0-0");
        db.StreamAdd(key1, "field2", "value2");
        db.StreamAdd(key1, "fieldLast", "valueLast");
        var entries = db.StreamRead(key1, "+");

        entries.Should().NotBeNull();
        (entries.Length > 0).Should().BeTrue();
        entries[0].Values.Should().Equal(new[] { new NameValueEntry("fieldLast", "valueLast") });
    }

    [Fact]
    public async Task stream_read_expected_exception_invalid_count_multiple_stream()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var streamPositions = new[]
        {
            new StreamPosition("key1", "0-0"),
            new StreamPosition("key2", "0-0"),
        };
        Assert.Throws<ArgumentOutOfRangeException>(() => db.StreamRead(streamPositions, 0));
    }

    [Fact]
    public async Task stream_read_expected_exception_invalid_count_single_stream()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();

        //Act
        var key = Me();

        //Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => db.StreamRead(key, "0-0", 0));
    }

    [Fact]
    public async Task stream_read_expected_exception_null_stream_list()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        //Act
        var db = conn.GetDatabase();

        //Assert
        Assert.Throws<ArgumentNullException>(() => db.StreamRead(null!));
    }

    [Fact]
    public async Task stream_read_expected_exception_empty_stream_list()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();

        //Act
        var emptyList = Array.Empty<StreamPosition>();

        //Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => db.StreamRead(emptyList));
    }

    [Fact]
    public async Task stream_read_multiple_streams()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key1 = Me() + "a";
        var key2 = Me() + "b";

        var id1 = db.StreamAdd(key1, "field1", "value1");
        var id2 = db.StreamAdd(key1, "field2", "value2");
        var id3 = db.StreamAdd(key2, "field3", "value3");
        var id4 = db.StreamAdd(key2, "field4", "value4");

        // Read from both streams at the same time.
        var streamList = new[]
        {
            new StreamPosition(key1, "0-0"),
            new StreamPosition(key2, "0-0"),
        };

        var streams = db.StreamRead(streamList);

        streams.Length.Should().Be(2);

        streams[0].Key.Should().Be(key1);
        streams[0].Entries.Length.Should().Be(2);
        streams[0].Entries[0].Id.Should().Be(id1);
        streams[0].Entries[1].Id.Should().Be(id2);

        streams[1].Key.Should().Be(key2);
        streams[1].Entries.Length.Should().Be(2);
        streams[1].Entries[0].Id.Should().Be(id3);
        streams[1].Entries[1].Id.Should().Be(id4);
    }

    [Fact]
    public async Task stream_read_multiple_streams_last_message()
    {
        await using var conn = Create(require: RedisFeatures.v7_4_0_rc1);

        var db = conn.GetDatabase();
        var key1 = Me() + "a";
        var key2 = Me() + "b";

        var id1 = db.StreamAdd(key1, "field1", "value1");
        var id2 = db.StreamAdd(key1, "field2", "value2");
        var id3 = db.StreamAdd(key2, "field3", "value3");
        var id4 = db.StreamAdd(key2, "field4", "value4");

        var streamList = new[] { new StreamPosition(key1, "0-0"), new StreamPosition(key2, "0-0") };
        db.StreamRead(streamList);

        var streams = db.StreamRead(streamList);

        db.StreamAdd(key1, "field5", "value5");
        db.StreamAdd(key1, "field6", "value6");
        db.StreamAdd(key2, "field7", "value7");
        db.StreamAdd(key2, "field8", "value8");

        streamList = [new StreamPosition(key1, "+"), new StreamPosition(key2, "+")];

        streams = db.StreamRead(streamList);

        streams.Should().NotBeNull();
        streams.Length.Should().Be(2);

        var stream1 = streams.Where(e => e.Key == key1).First();
        stream1.Entries.Should().NotBeNull();
        (stream1.Entries.Length > 0).Should().BeTrue();
        stream1.Entries[0].Values.Should().Equal(new[] { new NameValueEntry("field6", "value6") });

        var stream2 = streams.Where(e => e.Key == key2).First();
        stream2.Entries.Should().NotBeNull();
        (stream2.Entries.Length > 0).Should().BeTrue();
        stream2.Entries[0].Values.Should().Equal(new[] { new NameValueEntry("field8", "value8") });
    }

    [Fact]
    public async Task stream_read_multiple_streams_with_count()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key1 = Me() + "a";
        var key2 = Me() + "b";

        var id1 = db.StreamAdd(key1, "field1", "value1");
        db.StreamAdd(key1, "field2", "value2");
        var id3 = db.StreamAdd(key2, "field3", "value3");
        db.StreamAdd(key2, "field4", "value4");

        var streamList = new[]
        {
            new StreamPosition(key1, "0-0"),
            new StreamPosition(key2, "0-0"),
        };

        var streams = db.StreamRead(streamList, countPerStream: 1);

        // We should get both streams back.
        streams.Length.Should().Be(2);

        // Ensure we only got one message per stream.
        streams[0].Entries.Should().ContainSingle();
        streams[1].Entries.Should().ContainSingle();

        // Check the message IDs as well.
        streams[0].Entries[0].Id.Should().Be(id1);
        streams[1].Entries[0].Id.Should().Be(id3);
    }

    [Fact]
    public async Task stream_read_multiple_streams_with_read_past_second_stream()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key1 = Me() + "a";
        var key2 = Me() + "b";

        db.StreamAdd(key1, "field1", "value1");
        db.StreamAdd(key1, "field2", "value2");
        db.StreamAdd(key2, "field3", "value3");
        var id4 = db.StreamAdd(key2, "field4", "value4");

        var streamList = new[]
        {
            new StreamPosition(key1, "0-0"),

            // read past the end of stream # 2
            new StreamPosition(key2, id4),
        };

        var streams = db.StreamRead(streamList);

        // We should only get the first stream back.
        streams.Should().ContainSingle();

        streams[0].Key.Should().Be(key1);
        streams[0].Entries.Length.Should().Be(2);
    }

    [Fact]
    public async Task stream_read_multiple_streams_with_empty_response()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key1 = Me() + "a";
        var key2 = Me() + "b";

        db.StreamAdd(key1, "field1", "value1");
        var id2 = db.StreamAdd(key1, "field2", "value2");
        db.StreamAdd(key2, "field3", "value3");
        var id4 = db.StreamAdd(key2, "field4", "value4");

        var streamList = new[]
        {
            // Read past the end of both streams.
            new StreamPosition(key1, id2),
            new StreamPosition(key2, id4),
        };

        var streams = db.StreamRead(streamList);

        // We expect an empty response.
        streams.Should().BeEmpty();
    }

    [Fact]
    public async Task stream_read_past_end_of_stream()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();

        db.StreamAdd(key, "field1", "value1");
        var id2 = db.StreamAdd(key, "field2", "value2");

        // Read after the final ID in the stream, we expect an empty array as a response.
        var entries = db.StreamRead(key, id2);

        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task stream_read_range()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();

        var id1 = db.StreamAdd(key, "field1", "value1");
        var id2 = db.StreamAdd(key, "field2", "value2");

        //Act
        var entries = db.StreamRange(key);

        //Assert
        entries.Length.Should().Be(2);
        entries[0].Id.Should().Be(id1);
        entries[1].Id.Should().Be(id2);
    }

    [Fact]
    public async Task stream_read_range_of_empty_stream()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();

        var id1 = db.StreamAdd(key, "field1", "value1");
        var id2 = db.StreamAdd(key, "field2", "value2");

        var deleted = db.StreamDelete(key, [id1, id2]);

        //Act
        var entries = db.StreamRange(key);

        //Assert
        deleted.Should().Be(2);
        entries.Should().NotBeNull();
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task stream_read_range_with_count()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();

        var id1 = db.StreamAdd(key, "field1", "value1");
        db.StreamAdd(key, "field2", "value2");

        //Act
        var entries = db.StreamRange(key, count: 1);

        //Assert
        entries.Should().ContainSingle();
        entries[0].Id.Should().Be(id1);
    }

    [Fact]
    public async Task stream_read_range_reverse()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();

        var id1 = db.StreamAdd(key, "field1", "value1");
        var id2 = db.StreamAdd(key, "field2", "value2");

        //Act
        var entries = db.StreamRange(key, messageOrder: Order.Descending);

        //Assert
        entries.Length.Should().Be(2);
        entries[0].Id.Should().Be(id2);
        entries[1].Id.Should().Be(id1);
    }

    [Fact]
    public async Task stream_read_range_reverse_with_count()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();

        var id1 = db.StreamAdd(key, "field1", "value1");
        var id2 = db.StreamAdd(key, "field2", "value2");

        //Act
        var entries = db.StreamRange(key, id1, id2, 1, Order.Descending);

        //Assert
        entries.Should().ContainSingle();
        entries[0].Id.Should().Be(id2);
    }

    [Fact]
    public async Task stream_read_with_after_id_and_count_1()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();

        var id1 = db.StreamAdd(key, "field1", "value1");
        var id2 = db.StreamAdd(key, "field2", "value2");
        db.StreamAdd(key, "field3", "value3");

        // Only read a single item from the stream.
        var entries = db.StreamRead(key, id1, 1);

        entries.Should().ContainSingle();
        entries[0].Id.Should().Be(id2);
    }

    [Fact]
    public async Task stream_read_with_after_id_and_count_2()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();

        var id1 = db.StreamAdd(key, "field1", "value1");
        var id2 = db.StreamAdd(key, "field2", "value2");
        var id3 = db.StreamAdd(key, "field3", "value3");
        db.StreamAdd(key, "field4", "value4");

        // Read multiple items from the stream.
        var entries = db.StreamRead(key, id1, 2);

        entries.Length.Should().Be(2);
        entries[0].Id.Should().Be(id2);
        entries[1].Id.Should().Be(id3);
    }

    protected override string GetConfiguration() => "127.0.0.1:6379";

    [Fact]
    public async Task stream_trim_length()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();

        // Add a couple items and check length.
        db.StreamAdd(key, "field1", "value1");
        db.StreamAdd(key, "field2", "value2");
        db.StreamAdd(key, "field3", "value3");
        db.StreamAdd(key, "field4", "value4");

        var numRemoved = db.StreamTrim(key, 1);
        var len = db.StreamLength(key);

        numRemoved.Should().Be(3);
        len.Should().Be(1);
    }

    private static Version ForMode(StreamTrimMode mode, Version? defaultVersion = null) => mode switch
    {
        StreamTrimMode.KeepReferences => defaultVersion ?? RedisFeatures.v5_0_0,
        StreamTrimMode.Acknowledged => RedisFeatures.v8_2_0_rc1,
        StreamTrimMode.DeleteReferences => RedisFeatures.v8_2_0_rc1,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    [Theory]
    [InlineData(StreamTrimMode.KeepReferences)]
    [InlineData(StreamTrimMode.DeleteReferences)]
    [InlineData(StreamTrimMode.Acknowledged)]
    public void stream_trim_by_min_id(StreamTrimMode mode)
    {
        using var conn = Create(require: ForMode(mode, RedisFeatures.v6_2_0));

        var db = conn.GetDatabase();
        var key = Me() + ":" + mode;

        // Add a couple items and check length.
        db.StreamAdd(key, "field1", "value1", 1111111110);
        db.StreamAdd(key, "field2", "value2", 1111111111);
        db.StreamAdd(key, "field3", "value3", 1111111112);

        var numRemoved = db.StreamTrimByMinId(key, 1111111111, mode: mode);
        var len = db.StreamLength(key);

        numRemoved.Should().Be(1);
        len.Should().Be(2);
    }

    [Theory]
    [InlineData(StreamTrimMode.KeepReferences)]
    [InlineData(StreamTrimMode.DeleteReferences)]
    [InlineData(StreamTrimMode.Acknowledged)]
    public void stream_trim_by_min_id_with_approximate_and_limit(StreamTrimMode mode)
    {
        Assert.Skip("Flaky");

        using var conn = Create(require: ForMode(mode, RedisFeatures.v6_2_0));

        var db = conn.GetDatabase();
        var key = Me() + ":" + mode;

        const int maxLength = 100;
        const int limit = 10;

        // The behavior of ACKED etc is undefined when there are no consumer groups; or rather,
        // it *is* defined, but it is defined/implemented differently < and >= server 8.6
        // This *does* have the side-effect that the 3 modes behave the same in this test,
        // but: we're trying to test the API, not the server.
        const string groupName = "test_group", consumer = "consumer";
        db.StreamCreateConsumerGroup(key, groupName, StreamPosition.NewMessages);
        for (var i = 0; i < maxLength; i++)
        {
            db.StreamAdd(key, $"field", $"value", 1111111110 + i);
        }

        var entries = db.StreamReadGroup(
            key,
            groupName,
            consumer,
            StreamPosition.NewMessages);

        entries.Length.Should().Be(maxLength);

        var numRemoved = db.StreamTrimByMinId(key, 1111111110 + maxLength, useApproximateMaxLength: true, limit: limit, mode: mode);
        const int EXPECT_REMOVED = 0;
        var len = db.StreamLength(key);

        numRemoved.Should().Be(EXPECT_REMOVED);
        len.Should().Be(maxLength - EXPECT_REMOVED);
    }

    [Fact]
    public async Task stream_verify_length()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();

        // Add a couple items and check length.
        db.StreamAdd(key, "field1", "value1");
        db.StreamAdd(key, "field2", "value2");

        var len = db.StreamLength(key);

        len.Should().Be(2);
    }

    [Fact]
    public async Task add_with_approx_count_async()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();
        await db.StreamAddAsync(key, "field", "value", maxLength: 10, useApproximateMaxLength: true, flags: CommandFlags.None).ConfigureAwait(false);
    }

    [Theory]
    [InlineData(StreamTrimMode.KeepReferences)]
    [InlineData(StreamTrimMode.DeleteReferences)]
    [InlineData(StreamTrimMode.Acknowledged)]
    public async Task add_with_approx_count(StreamTrimMode mode)
    {
        await using var conn = Create(require: ForMode(mode));

        var db = conn.GetDatabase();
        var key = Me() + ":" + mode;
        db.StreamAdd(key, "field", "value", maxLength: 10, useApproximateMaxLength: true, trimMode: mode, flags: CommandFlags.None);
    }

    [Theory]
    [InlineData(StreamTrimMode.KeepReferences, 1)]
    [InlineData(StreamTrimMode.DeleteReferences, 1)]
    [InlineData(StreamTrimMode.Acknowledged, 1)]
    [InlineData(StreamTrimMode.KeepReferences, 2)]
    [InlineData(StreamTrimMode.DeleteReferences, 2)]
    [InlineData(StreamTrimMode.Acknowledged, 2)]
    public async Task add_with_multiple_approx_count(StreamTrimMode mode, int count)
    {
        await using var conn = Create(require: ForMode(mode));

        var db = conn.GetDatabase();
        var key = Me() + ":" + mode;

        var pairs = new NameValueEntry[count];
        for (var i = 0; i < count; i++)
        {
            pairs[i] = new NameValueEntry($"field{i}", $"value{i}");
        }
        db.StreamAdd(key, maxLength: 10, useApproximateMaxLength: true, trimMode: mode, flags: CommandFlags.None, streamPairs: pairs);
    }

    [Fact]
    public async Task stream_read_group_with_no_ack_shows_no_pending_messages()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string groupName = "test_group",
                     consumer = "consumer";

        db.StreamAdd(key, "field1", "value1");
        db.StreamAdd(key, "field2", "value2");

        db.StreamCreateConsumerGroup(key, groupName, StreamPosition.NewMessages);

        db.StreamReadGroup(
            key,
            groupName,
            consumer,
            StreamPosition.NewMessages,
            noAck: true);

        //Act
        var pendingInfo = db.StreamPending(key, groupName);

        //Assert
        pendingInfo.PendingMessageCount.Should().Be(0);
    }

    [Fact]
    public async Task stream_read_group_multi_stream_with_no_ack_shows_no_pending_messages()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key1 = Me() + "a";
        var key2 = Me() + "b";
        const string groupName = "test_group",
                     consumer = "consumer";

        db.StreamAdd(key1, "field1", "value1");
        db.StreamAdd(key1, "field2", "value2");

        db.StreamAdd(key2, "field3", "value3");
        db.StreamAdd(key2, "field4", "value4");

        db.StreamCreateConsumerGroup(key1, groupName, StreamPosition.NewMessages);
        db.StreamCreateConsumerGroup(key2, groupName, StreamPosition.NewMessages);

        db.StreamReadGroup(
            [
                new StreamPosition(key1, StreamPosition.NewMessages),
                new StreamPosition(key2, StreamPosition.NewMessages),
            ],
            groupName,
            consumer,
            noAck: true);

        var pending1 = db.StreamPending(key1, groupName);

        //Act
        var pending2 = db.StreamPending(key2, groupName);

        //Assert
        pending1.PendingMessageCount.Should().Be(0);
        pending2.PendingMessageCount.Should().Be(0);
    }

    [Fact]
    public async Task stream_read_indexer_usage()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var streamName = Me();

        await db.StreamAddAsync(
            streamName,
            [
                new NameValueEntry("x", "blah"),
                new NameValueEntry("msg", /*lang=json,strict*/ @"{""name"":""test"",""id"":123}"),
                new NameValueEntry("y", "more blah"),
            ]);

        var streamResult = await db.StreamRangeAsync(streamName, count: 1000);

        //Act
        var evntJson = streamResult
            .Select(x => JsonNode.Parse((string)x["msg"]!)!)
            .ToList();

        //Assert
        var obj = Assert.Single(evntJson);
        ((int)obj["id"]!).Should().Be(123);
        ((string?)obj["name"]!).Should().Be("test");
    }

    [Fact]
    public async Task stream_consumer_group_info_lag_is_null()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string groupName = "test_group",
                     consumer = "consumer";

        await db.StreamCreateConsumerGroupAsync(key, groupName);
        await db.StreamReadGroupAsync(key, groupName, consumer, "0-0", 1);
        await db.StreamAddAsync(key, "field1", "value1");
        await db.StreamAddAsync(key, "field1", "value1");

        var streamInfo = await db.StreamInfoAsync(key);
        await db.StreamDeleteAsync(key, new[] { streamInfo.LastEntry.Id });

        ((await db.StreamGroupInfoAsync(key))[0].Lag).Should().BeNull();
    }

    [Fact]
    public async Task stream_consumer_group_info_lag_is_two()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string groupName = "test_group",
                     consumer = "consumer";

        await db.StreamCreateConsumerGroupAsync(key, groupName);
        await db.StreamReadGroupAsync(key, groupName, consumer, "0-0", 1);
        await db.StreamAddAsync(key, "field1", "value1");

        //Act
        await db.StreamAddAsync(key, "field1", "value1");

        //Assert
        ((await db.StreamGroupInfoAsync(key))[0].Lag).Should().Be(2);
    }
}
