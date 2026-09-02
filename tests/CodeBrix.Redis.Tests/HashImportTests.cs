using System;
using System.Threading.Tasks;
using CodeBrix.Redis.KeyspaceIsolation;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

/// <summary>
/// Integration tests for <see cref="IDatabase.HashImport"/> / <see cref="IDatabaseAsync.HashImportAsync"/> and the
/// reusable <see cref="HashImport"/> field-set (the session-based <c>HIMPORT</c> feature, Redis 8.10+).
/// </summary>
[RunPerProtocol]
public class HashImportTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    private static RedisValue[] Values(string name, string email, int age) => [name, email, age];

    [Fact]
    public async Task imports_many_hashes_reusing_one_field_set()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v8_10_0);
        var db = conn.GetDatabase();
        var prefix = Me();
        RedisKey k1 = prefix + ":1", k2 = prefix + ":2", k3 = prefix + ":3";
        await db.KeyDeleteAsync([k1, k2, k3]);
        await using var fieldSet = HashImport.Create("name", "email", "age");
        await db.HashImportAsync(k1, fieldSet, Values("alice", "a@example.com", 30));
        await db.HashImportAsync(k2, fieldSet, Values("bob", "b@example.com", 25));

        //Act
        await db.HashImportAsync(k3, fieldSet, Values("carol", "c@example.com", 42));

        //Assert
        (await db.HashGetAsync(k1, "name")).Should().Be("alice");
        (await db.HashGetAsync(k1, "email")).Should().Be("a@example.com");
        ((int)await db.HashGetAsync(k1, "age")).Should().Be(30);
        (await db.HashGetAsync(k2, "name")).Should().Be("bob");
        (await db.HashGetAsync(k3, "name")).Should().Be("carol");
        (await db.HashLengthAsync(k3)).Should().Be(3);
    }

    [Fact]
    public async Task single_entry_works()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v8_10_0);
        var db = conn.GetDatabase();
        RedisKey key = Me();
        await db.KeyDeleteAsync(key);
        await using var fieldSet = HashImport.Create("name", "email", "age");

        //Act
        await db.HashImportAsync(key, fieldSet, Values("alice", "a@example.com", 30));

        //Assert
        (await db.HashGetAsync(key, "name")).Should().Be("alice");
        (await db.HashLengthAsync(key)).Should().Be(3);
    }

    [Fact]
    public async Task existing_hash_is_replaced_not_merged()
    {
        await using var conn = Create(require: RedisFeatures.v8_10_0);
        var db = conn.GetDatabase();
        RedisKey key = Me();
        await db.KeyDeleteAsync(key);
        await db.HashSetAsync(key, [new("old", 1), new("keep", 2)]);

        await using var fieldSet = HashImport.Create("name", "email", "age");
        await db.HashImportAsync(key, fieldSet, Values("alice", "a@x", 30));

        // HIMPORT SET replaces the whole hash: the pre-existing 'old'/'keep' fields are gone
        (await db.HashLengthAsync(key)).Should().Be(3);
        (await db.HashExistsAsync(key, "old")).Should().BeFalse();
        (await db.HashGetAsync(key, "name")).Should().Be("alice");
    }

    [Fact]
    public async Task mismatched_value_count_throws_before_sending()
    {
        await using var conn = Create(require: RedisFeatures.v8_10_0);
        var db = conn.GetDatabase();
        await using var fieldSet = HashImport.Create("name", "email", "age");
        // 3 fields but only 1 value -> synchronous ArgumentException, before any Task is returned
        Assert.Throws<ArgumentException>(() => { _ = db.HashImportAsync(Me(), fieldSet, new RedisValue[] { "only-one" }); });
    }

    [Fact]
    public async Task null_field_set_throws()
    {
        await using var conn = Create(require: RedisFeatures.v8_10_0);
        var db = conn.GetDatabase();
        Assert.Throws<ArgumentNullException>(() => { _ = db.HashImportAsync(Me(), null!, new RedisValue[] { "x" }); });
    }

    [Fact]
    public async Task wrong_type_key_throws_but_other_keys_succeed()
    {
        await using var conn = Create(require: RedisFeatures.v8_10_0);
        var db = conn.GetDatabase();
        var prefix = Me();
        RedisKey k0 = prefix + ":0", k1 = prefix + ":1";
        await db.KeyDeleteAsync([k0, k1]);
        await db.StringSetAsync(k0, "i-am-a-string"); // wrong type for a hash import

        await using var fieldSet = HashImport.Create("name", "email", "age");

        var ex = await Assert.ThrowsAsync<RedisServerException>(() => db.HashImportAsync(k0, fieldSet, Values("alice", "a@x", 30)));
        ex.Message.Should().StartWith("WRONGTYPE");

        // each import is applied on its own: a later valid key still succeeds despite the earlier failure
        await db.HashImportAsync(k1, fieldSet, Values("bob", "b@x", 25));
        (await db.HashGetAsync(k1, "name")).Should().Be("bob");
        (await db.StringGetAsync(k0)).Should().Be("i-am-a-string"); // untouched
    }

    [Fact]
    public void duplicate_field_names_rejected_at_create()
    {
        // rejected client-side: the server would reject the PREPARE, but that is injected fire-and-forget and would
        // only surface indirectly as a "no such fieldset" failure on every SET - so we fail fast at the mistake.
        var ex = Assert.Throws<ArgumentException>(() => HashImport.Create("f1", "f1"));
        ex.Message.Should().Contain("Duplicate field name");
    }

    [Fact]
    public async Task fields_are_snapshot_at_create()
    {
        await using var conn = Create(require: RedisFeatures.v8_10_0);
        var db = conn.GetDatabase();
        RedisKey key = Me();
        await db.KeyDeleteAsync(key);

        var fieldNames = new RedisValue[] { "name", "email", "age" };
        await using var fieldSet = HashImport.Create(fieldNames);
        fieldNames[0] = "MUTATED"; // mutate the caller's array after Create - must not affect the field-set

        await db.HashImportAsync(key, fieldSet, Values("alice", "a@x", 30));
        (await db.HashGetAsync(key, "name")).Should().Be("alice"); // still 'name', not 'MUTATED'
        (await db.HashExistsAsync(key, "MUTATED")).Should().BeFalse();
    }

    [Fact]
    public void null_field_name_rejected_at_create()
    {
        var ex = Assert.Throws<ArgumentException>(() => HashImport.Create("a", RedisValue.Null));
        ex.Message.Should().Contain("null");
    }

    [Fact]
    public void empty_field_name_allowed()
    {
        // the server accepts an empty field name (a hash can legitimately have an empty-string field), so we do too
        using var fieldSet = HashImport.Create("", "b");
        fieldSet.Should().NotBeNull();
    }

    [Fact]
    public async Task key_prefix_isolation_prefixes_key()
    {
        await using var conn = Create(require: RedisFeatures.v8_10_0);
        var prefix = Me() + ":";
        var db = conn.GetDatabase().WithKeyPrefix(prefix);
        var raw = conn.GetDatabase();

        const string inner = "u1";
        string full = prefix + inner;
        await raw.KeyDeleteAsync(full);

        await using var fieldSet = HashImport.Create("name", "email", "age");
        await db.HashImportAsync(inner, fieldSet, Values("alice", "a@x", 30));

        // written under the prefixed key
        (await raw.HashGetAsync(full, "name")).Should().Be("alice");
    }

    [Fact]
    public async Task works_inside_batch()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v8_10_0);
        var db = conn.GetDatabase();
        var prefix = Me();
        RedisKey k1 = prefix + ":1", k2 = prefix + ":2";
        await db.KeyDeleteAsync([k1, k2]);
        await using var fieldSet = HashImport.Create("name", "email", "age");
        var batch = db.CreateBatch();
        var t1 = batch.HashImportAsync(k1, fieldSet, Values("alice", "a@x", 30));
        var t2 = batch.HashImportAsync(k2, fieldSet, Values("bob", "b@x", 25));
        batch.Execute();

        //Act
        await Task.WhenAll(t1, t2);

        //Assert
        (await db.HashGetAsync(k1, "name")).Should().Be("alice");
        (await db.HashGetAsync(k2, "name")).Should().Be("bob");
    }

    [Fact]
    public async Task use_after_dispose_throws()
    {
        await using var conn = Create(require: RedisFeatures.v8_10_0);
        var db = conn.GetDatabase();
        var fieldSet = HashImport.Create("name", "email", "age");
        fieldSet.Dispose();
        // rejected before anything is sent (the field-set may already have been DISCARDed on the server)
        Assert.Throws<ObjectDisposedException>(() => { _ = db.HashImportAsync(Me(), fieldSet, Values("a", "b", 1)); });
    }

    [Fact]
    public async Task double_dispose_is_no_op()
    {
        await using var conn = Create(require: RedisFeatures.v8_10_0);
        var db = conn.GetDatabase();
        RedisKey key = Me();
        await db.KeyDeleteAsync(key);

        var fieldSet = HashImport.Create("name", "email", "age");
        await db.HashImportAsync(key, fieldSet, Values("alice", "a@x", 30));
        fieldSet.Dispose();
        fieldSet.Dispose(); // idempotent: no second DISCARD, no throw
        await fieldSet.DisposeAsync(); // also idempotent across the sync/async forms

        (await db.HashGetAsync(key, "name")).Should().Be("alice");
    }

    [Fact]
    public async Task not_supported_inside_transaction()
    {
        await using var conn = Create(require: RedisFeatures.v8_10_0);
        var tran = conn.GetDatabase().CreateTransaction();
        await using var fieldSet = HashImport.Create("name", "email", "age");
        // the transaction guard runs synchronously
        Assert.Throws<NotSupportedException>(() => { _ = tran.HashImportAsync(Me(), fieldSet, Values("a", "b", 1)); });
    }
}
