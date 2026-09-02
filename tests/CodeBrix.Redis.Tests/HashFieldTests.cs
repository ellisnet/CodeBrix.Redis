using System;
using System.Linq;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

/// <summary>
/// Tests for <see href="https://redis.io/commands#hash"/>.
/// </summary>
[RunPerProtocol]
public class HashFieldTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    private readonly DateTime nextCentury = new DateTime(2101, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly TimeSpan oneYearInMs = TimeSpan.FromMilliseconds(31536000000);

    private readonly HashEntry[] entries = [new("f1", 1), new("f2", 2)];

    private readonly RedisValue[] fields = ["f1", "f2"];

    private readonly RedisValue[] values = [1, 2];

    [Fact]
    public void hash_field_expire()
    {
        var db = Create(require: RedisFeatures.v7_4_0_rc1).GetDatabase();
        var hashKey = Me();
        db.HashSet(hashKey, entries);

        var fieldsResult = db.HashFieldExpire(hashKey, fields, oneYearInMs);
        fieldsResult.Should().Equal([ExpireResult.Success, ExpireResult.Success]);

        fieldsResult = db.HashFieldExpire(hashKey, fields, nextCentury);
        fieldsResult.Should().Equal([ExpireResult.Success, ExpireResult.Success,]);
    }

    [Fact]
    public void hash_field_expire_no_key()
    {
        var db = Create(require: RedisFeatures.v7_4_0_rc2).GetDatabase();
        var hashKey = Me();

        var fieldsResult = db.HashFieldExpire(hashKey, fields, oneYearInMs);
        fieldsResult.Should().Equal([ExpireResult.NoSuchField, ExpireResult.NoSuchField]);

        fieldsResult = db.HashFieldExpire(hashKey, fields, nextCentury);
        fieldsResult.Should().Equal([ExpireResult.NoSuchField, ExpireResult.NoSuchField]);
    }

    [Fact]
    public async Task hash_field_expire_async()
    {
        var db = Create(require: RedisFeatures.v7_4_0_rc1).GetDatabase();
        var hashKey = Me();
        db.HashSet(hashKey, entries);

        var fieldsResult = await db.HashFieldExpireAsync(hashKey, fields, oneYearInMs);
        fieldsResult.Should().Equal([ExpireResult.Success, ExpireResult.Success]);

        fieldsResult = await db.HashFieldExpireAsync(hashKey, fields, nextCentury);
        fieldsResult.Should().Equal([ExpireResult.Success, ExpireResult.Success]);
    }

    [Fact]
    public async Task hash_field_expire_async_no_key()
    {
        var db = Create(require: RedisFeatures.v7_4_0_rc2).GetDatabase();
        var hashKey = Me();

        var fieldsResult = await db.HashFieldExpireAsync(hashKey, fields, oneYearInMs);
        fieldsResult.Should().Equal([ExpireResult.NoSuchField, ExpireResult.NoSuchField]);

        fieldsResult = await db.HashFieldExpireAsync(hashKey, fields, nextCentury);
        fieldsResult.Should().Equal([ExpireResult.NoSuchField, ExpireResult.NoSuchField]);
    }

    [Fact]
    public void hash_field_get_expire_date_time_is_due()
    {
        //Arrange
        var db = Create(require: RedisFeatures.v7_4_0_rc1).GetDatabase();
        var hashKey = Me();
        db.HashSet(hashKey, entries);

        //Act
        var result = db.HashFieldExpire(hashKey, ["f1"], new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        //Assert
        result.Should().Equal([ExpireResult.Due]);
    }

    [Fact]
    public void hash_field_expire_no_field()
    {
        //Arrange
        var db = Create(require: RedisFeatures.v7_4_0_rc1).GetDatabase();
        var hashKey = Me();
        db.HashSet(hashKey, entries);

        //Act
        var result = db.HashFieldExpire(hashKey, ["nonExistingField"], oneYearInMs);

        //Assert
        result.Should().Equal([ExpireResult.NoSuchField]);
    }

    [Fact]
    public void hash_field_expire_conditions_satisfied()
    {
        var db = Create(require: RedisFeatures.v7_4_0_rc1).GetDatabase();
        var hashKey = Me();
        db.KeyDelete(hashKey);
        db.HashSet(hashKey, entries);
        db.HashSet(hashKey, [new("f3", 3), new("f4", 4)]);
        var initialExpire = db.HashFieldExpire(hashKey, ["f2", "f3", "f4"], new DateTime(2050, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        initialExpire.Should().Equal([ExpireResult.Success, ExpireResult.Success, ExpireResult.Success]);

        var result = db.HashFieldExpire(hashKey, ["f1"], oneYearInMs, ExpireWhen.HasNoExpiry);
        result.Should().Equal([ExpireResult.Success]);

        result = db.HashFieldExpire(hashKey, ["f2"], oneYearInMs, ExpireWhen.HasExpiry);
        result.Should().Equal([ExpireResult.Success]);

        result = db.HashFieldExpire(hashKey, ["f3"], nextCentury, ExpireWhen.GreaterThanCurrentExpiry);
        result.Should().Equal([ExpireResult.Success]);

        result = db.HashFieldExpire(hashKey, ["f4"], oneYearInMs, ExpireWhen.LessThanCurrentExpiry);
        result.Should().Equal([ExpireResult.Success]);
    }

    [Fact]
    public void hash_field_expire_conditions_not_satisfied()
    {
        var db = Create(require: RedisFeatures.v7_4_0_rc1).GetDatabase();
        var hashKey = Me();
        db.KeyDelete(hashKey);
        db.HashSet(hashKey, entries);
        db.HashSet(hashKey, [new("f3", 3), new("f4", 4)]);
        var initialExpire = db.HashFieldExpire(hashKey, ["f2", "f3", "f4"], new DateTime(2050, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        initialExpire.Should().Equal([ExpireResult.Success, ExpireResult.Success, ExpireResult.Success]);

        var result = db.HashFieldExpire(hashKey, ["f1"], oneYearInMs, ExpireWhen.HasExpiry);
        result.Should().Equal([ExpireResult.ConditionNotMet]);

        result = db.HashFieldExpire(hashKey, ["f2"], oneYearInMs, ExpireWhen.HasNoExpiry);
        result.Should().Equal([ExpireResult.ConditionNotMet]);

        result = db.HashFieldExpire(hashKey, ["f3"], nextCentury, ExpireWhen.LessThanCurrentExpiry);
        result.Should().Equal([ExpireResult.ConditionNotMet]);

        result = db.HashFieldExpire(hashKey, ["f4"], oneYearInMs, ExpireWhen.GreaterThanCurrentExpiry);
        result.Should().Equal([ExpireResult.ConditionNotMet]);
    }

    [Fact]
    public void hash_field_get_expire_date_time()
    {
        var db = Create(require: RedisFeatures.v7_4_0_rc1).GetDatabase();
        var hashKey = Me();
        db.HashSet(hashKey, entries);
        db.HashFieldExpire(hashKey, fields, nextCentury);
        long ms = new DateTimeOffset(nextCentury).ToUnixTimeMilliseconds();

        var result = db.HashFieldGetExpireDateTime(hashKey, ["f1"]);
        result.Should().Equal([ms]);

        var fieldsResult = db.HashFieldGetExpireDateTime(hashKey, fields);
        fieldsResult.Should().Equal([ms, ms]);
    }

    [Fact]
    public void hash_field_expire_field_no_expire_time()
    {
        var db = Create(require: RedisFeatures.v7_4_0_rc1).GetDatabase();
        var hashKey = Me();
        db.HashSet(hashKey, entries);

        var result = db.HashFieldGetExpireDateTime(hashKey, ["f1"]);
        result.Should().Equal([-1L]);

        var fieldsResult = db.HashFieldGetExpireDateTime(hashKey, fields);
        fieldsResult.Should().Equal([-1, -1,]);
    }

    [Fact]
    public void hash_field_get_expire_date_time_no_key()
    {
        //Arrange
        var db = Create(require: RedisFeatures.v7_4_0_rc2).GetDatabase();
        var hashKey = Me();

        //Act
        var fieldsResult = db.HashFieldGetExpireDateTime(hashKey, fields);

        //Assert
        fieldsResult.Should().Equal([-2, -2,]);
    }

    [Fact]
    public void hash_field_get_expire_date_time_no_field()
    {
        //Arrange
        var db = Create(require: RedisFeatures.v7_4_0_rc1).GetDatabase();
        var hashKey = Me();
        db.HashSet(hashKey, entries);
        db.HashFieldExpire(hashKey, fields, oneYearInMs);

        //Act
        var fieldsResult = db.HashFieldGetExpireDateTime(hashKey, ["notExistingField1", "notExistingField2"]);

        //Assert
        fieldsResult.Should().Equal([-2, -2,]);
    }

    [Fact]
    public void hash_field_get_time_to_live()
    {
        var db = Create(require: RedisFeatures.v7_4_0_rc1).GetDatabase();
        var hashKey = Me();
        db.HashSet(hashKey, entries);
        db.HashFieldExpire(hashKey, fields, oneYearInMs);
        long ms = new DateTimeOffset(nextCentury).ToUnixTimeMilliseconds();

        var result = db.HashFieldGetTimeToLive(hashKey, ["f1"]);
        result.Should().NotBeNull();
        (result.Length == 1).Should().BeTrue();
        (result[0] > 0).Should().BeTrue();

        var fieldsResult = db.HashFieldGetTimeToLive(hashKey, fields);
        fieldsResult.Should().NotBeNull();
        (fieldsResult.Length > 0).Should().BeTrue();
        fieldsResult.All(x => x > 0).Should().BeTrue();
    }

    [Fact]
    public void hash_field_get_time_to_live_no_expire_time()
    {
        //Arrange
        var db = Create(require: RedisFeatures.v7_4_0_rc1).GetDatabase();
        var hashKey = Me();
        db.HashSet(hashKey, entries);

        //Act
        var fieldsResult = db.HashFieldGetTimeToLive(hashKey, fields);

        //Assert
        fieldsResult.Should().Equal([-1, -1,]);
    }

    [Fact]
    public void hash_field_get_time_to_live_no_key()
    {
        //Arrange
        var db = Create(require: RedisFeatures.v7_4_0_rc2).GetDatabase();
        var hashKey = Me();

        //Act
        var fieldsResult = db.HashFieldGetTimeToLive(hashKey, fields);

        //Assert
        fieldsResult.Should().Equal([-2, -2,]);
    }

    [Fact]
    public void hash_field_get_time_to_live_no_field()
    {
        //Arrange
        var db = Create(require: RedisFeatures.v7_4_0_rc1).GetDatabase();
        var hashKey = Me();
        db.HashSet(hashKey, entries);
        db.HashFieldExpire(hashKey, fields, oneYearInMs);

        //Act
        var fieldsResult = db.HashFieldGetTimeToLive(hashKey, ["notExistingField1", "notExistingField2"]);

        //Assert
        fieldsResult.Should().Equal([-2, -2,]);
    }

    [Fact]
    public void hash_field_persist()
    {
        var db = Create(require: RedisFeatures.v7_4_0_rc1).GetDatabase();
        var hashKey = Me();
        db.HashSet(hashKey, entries);
        db.HashFieldExpire(hashKey, fields, oneYearInMs);
        long ms = new DateTimeOffset(nextCentury).ToUnixTimeMilliseconds();

        var result = db.HashFieldPersist(hashKey, ["f1"]);
        result.Should().Equal([PersistResult.Success]);

        db.HashFieldExpire(hashKey, fields, oneYearInMs);

        var fieldsResult = db.HashFieldPersist(hashKey, fields);
        fieldsResult.Should().Equal([PersistResult.Success, PersistResult.Success]);
    }

    [Fact]
    public void hash_field_persist_no_expire_time()
    {
        //Arrange
        var db = Create(require: RedisFeatures.v7_4_0_rc1).GetDatabase();
        var hashKey = Me();
        db.HashSet(hashKey, entries);

        //Act
        var fieldsResult = db.HashFieldPersist(hashKey, fields);

        //Assert
        fieldsResult.Should().Equal([PersistResult.ConditionNotMet, PersistResult.ConditionNotMet]);
    }

    [Fact]
    public void hash_field_persist_no_key()
    {
        //Arrange
        var db = Create(require: RedisFeatures.v7_4_0_rc2).GetDatabase();
        var hashKey = Me();

        //Act
        var fieldsResult = db.HashFieldPersist(hashKey, fields);

        //Assert
        fieldsResult.Should().Equal([PersistResult.NoSuchField, PersistResult.NoSuchField]);
    }

    [Fact]
    public void hash_field_persist_no_field()
    {
        //Arrange
        var db = Create(require: RedisFeatures.v7_4_0_rc1).GetDatabase();
        var hashKey = Me();
        db.HashSet(hashKey, entries);
        db.HashFieldExpire(hashKey, fields, oneYearInMs);

        //Act
        var fieldsResult = db.HashFieldPersist(hashKey, ["notExistingField1", "notExistingField2"]);

        //Assert
        fieldsResult.Should().Equal([PersistResult.NoSuchField, PersistResult.NoSuchField]);
    }

    [Fact]
    public void hash_field_get_and_set_expiry()
    {
        using var conn = Create(require: RedisFeatures.v8_0_0_M04);
        var db = conn.GetDatabase();
        var hashKey = Me();

        // testing with timespan
        db.HashSet(hashKey, entries);
        var fieldResult = db.HashFieldGetAndSetExpiry(hashKey, "f1", TimeSpan.FromHours(1));
        fieldResult.Should().Be(1);
        var fieldTtl = db.HashFieldGetTimeToLive(hashKey, new RedisValue[] { "f1" })[0];
        ((double)fieldTtl).Should().BeInRange(TimeSpan.FromMinutes(59).TotalMilliseconds, TimeSpan.FromHours(1).TotalMilliseconds);

        // testing with datetime
        db.HashSet(hashKey, entries);
        fieldResult = db.HashFieldGetAndSetExpiry(hashKey, "f1", DateTime.Now.AddMinutes(120));
        fieldResult.Should().Be(1);
        fieldTtl = db.HashFieldGetTimeToLive(hashKey, new RedisValue[] { "f1" })[0];
        ((double)fieldTtl).Should().BeInRange(TimeSpan.FromMinutes(119).TotalMilliseconds, TimeSpan.FromHours(2).TotalMilliseconds);

        // testing persist
        fieldResult = db.HashFieldGetAndSetExpiry(hashKey, "f1", persist: true);
        fieldResult.Should().Be(1);
        fieldTtl = db.HashFieldGetTimeToLive(hashKey, new RedisValue[] { "f1" })[0];
        fieldTtl.Should().Be(-1);

        // testing multiple fields with timespan
        db.HashSet(hashKey, entries);
        var fieldResults = db.HashFieldGetAndSetExpiry(hashKey, fields, TimeSpan.FromHours(1));
        fieldResults.Should().Equal(values);
        var fieldTtls = db.HashFieldGetTimeToLive(hashKey, fields);
        ((double)fieldTtls[0]).Should().BeInRange(TimeSpan.FromMinutes(59).TotalMilliseconds, TimeSpan.FromHours(1).TotalMilliseconds);
        ((double)fieldTtls[1]).Should().BeInRange(TimeSpan.FromMinutes(59).TotalMilliseconds, TimeSpan.FromHours(1).TotalMilliseconds);

        // testing multiple fields with datetime
        db.HashSet(hashKey, entries);
        fieldResults = db.HashFieldGetAndSetExpiry(hashKey, fields, DateTime.Now.AddMinutes(120));
        fieldResults.Should().Equal(values);
        fieldTtls = db.HashFieldGetTimeToLive(hashKey, fields);
        ((double)fieldTtls[0]).Should().BeInRange(TimeSpan.FromMinutes(119).TotalMilliseconds, TimeSpan.FromHours(2).TotalMilliseconds);
        ((double)fieldTtls[1]).Should().BeInRange(TimeSpan.FromMinutes(119).TotalMilliseconds, TimeSpan.FromHours(2).TotalMilliseconds);

        // testing multiple fields with persist
        fieldResults = db.HashFieldGetAndSetExpiry(hashKey, fields, persist: true);
        fieldResults.Should().Equal(values);
        fieldTtls = db.HashFieldGetTimeToLive(hashKey, fields);
        fieldTtls.Should().Equal(new long[] { -1, -1 });
    }

    [Fact]
    public async Task hash_field_get_and_set_expiry_async()
    {
        await using var conn = Create(require: RedisFeatures.v8_0_0_M04);
        var db = conn.GetDatabase();
        var hashKey = Me();

        // testing with timespan
        db.HashSet(hashKey, entries);
        var fieldResult = await db.HashFieldGetAndSetExpiryAsync(hashKey, "f1", TimeSpan.FromHours(1));
        fieldResult.Should().Be(1);
        var fieldTtl = db.HashFieldGetTimeToLive(hashKey, new RedisValue[] { "f1" })[0];
        ((double)fieldTtl).Should().BeInRange(TimeSpan.FromMinutes(59).TotalMilliseconds, TimeSpan.FromHours(1).TotalMilliseconds);

        // testing with datetime
        db.HashSet(hashKey, entries);
        fieldResult = await db.HashFieldGetAndSetExpiryAsync(hashKey, "f1", DateTime.Now.AddMinutes(120));
        fieldResult.Should().Be(1);
        fieldTtl = db.HashFieldGetTimeToLive(hashKey, new RedisValue[] { "f1" })[0];
        ((double)fieldTtl).Should().BeInRange(TimeSpan.FromMinutes(119).TotalMilliseconds, TimeSpan.FromHours(2).TotalMilliseconds);

        // testing persist
        fieldResult = await db.HashFieldGetAndSetExpiryAsync(hashKey, "f1", persist: true);
        fieldResult.Should().Be(1);
        fieldTtl = db.HashFieldGetTimeToLive(hashKey, new RedisValue[] { "f1" })[0];
        fieldTtl.Should().Be(-1);

        // testing multiple fields with timespan
        db.HashSet(hashKey, entries);
        var fieldResults = await db.HashFieldGetAndSetExpiryAsync(hashKey, fields, TimeSpan.FromHours(1));
        fieldResults.Should().Equal(values);
        var fieldTtls = db.HashFieldGetTimeToLive(hashKey, fields);
        ((double)fieldTtls[0]).Should().BeInRange(TimeSpan.FromMinutes(59).TotalMilliseconds, TimeSpan.FromHours(1).TotalMilliseconds);
        ((double)fieldTtls[1]).Should().BeInRange(TimeSpan.FromMinutes(59).TotalMilliseconds, TimeSpan.FromHours(1).TotalMilliseconds);

        // testing multiple fields with datetime
        db.HashSet(hashKey, entries);
        fieldResults = await db.HashFieldGetAndSetExpiryAsync(hashKey, fields, DateTime.Now.AddMinutes(120));
        fieldResults.Should().Equal(values);
        fieldTtls = db.HashFieldGetTimeToLive(hashKey, fields);
        ((double)fieldTtls[0]).Should().BeInRange(TimeSpan.FromMinutes(119).TotalMilliseconds, TimeSpan.FromHours(2).TotalMilliseconds);
        ((double)fieldTtls[1]).Should().BeInRange(TimeSpan.FromMinutes(119).TotalMilliseconds, TimeSpan.FromHours(2).TotalMilliseconds);

        // testing multiple fields with persist
        fieldResults = await db.HashFieldGetAndSetExpiryAsync(hashKey, fields, persist: true);
        fieldResults.Should().Equal(values);
        fieldTtls = db.HashFieldGetTimeToLive(hashKey, fields);
        fieldTtls.Should().Equal(new long[] { -1, -1 });
    }

    [Fact]
    public void hash_field_set_and_set_expiry()
    {
        using var conn = Create(require: RedisFeatures.v8_0_0_M04);
        var db = conn.GetDatabase();
        var hashKey = Me();

        // testing with timespan
        var result = db.HashFieldSetAndSetExpiry(hashKey, "f1", 1, TimeSpan.FromHours(1));
        result.Should().Be(1);
        var fieldTtl = db.HashFieldGetTimeToLive(hashKey, new RedisValue[] { "f1" })[0];
        ((double)fieldTtl).Should().BeInRange(TimeSpan.FromMinutes(59).TotalMilliseconds, TimeSpan.FromHours(1).TotalMilliseconds);

        // testing with datetime
        result = db.HashFieldSetAndSetExpiry(hashKey, "f1", 1, DateTime.Now.AddMinutes(120));
        result.Should().Be(1);
        fieldTtl = db.HashFieldGetTimeToLive(hashKey, new RedisValue[] { "f1" })[0];
        ((double)fieldTtl).Should().BeInRange(TimeSpan.FromMinutes(119).TotalMilliseconds, TimeSpan.FromHours(2).TotalMilliseconds);

        // testing with keepttl
        result = db.HashFieldSetAndSetExpiry(hashKey, "f1", 1, keepTtl: true);
        result.Should().Be(1);
        fieldTtl = db.HashFieldGetTimeToLive(hashKey, new RedisValue[] { "f1" })[0];
        ((double)fieldTtl).Should().BeInRange(TimeSpan.FromMinutes(119).TotalMilliseconds, TimeSpan.FromHours(2).TotalMilliseconds);

        // testing multiple fields with timespan
        result = db.HashFieldSetAndSetExpiry(hashKey, entries, TimeSpan.FromHours(1));
        result.Should().Be(1);
        var fieldTtls = db.HashFieldGetTimeToLive(hashKey, fields);
        ((double)fieldTtls[0]).Should().BeInRange(TimeSpan.FromMinutes(59).TotalMilliseconds, TimeSpan.FromHours(1).TotalMilliseconds);
        ((double)fieldTtls[1]).Should().BeInRange(TimeSpan.FromMinutes(59).TotalMilliseconds, TimeSpan.FromHours(1).TotalMilliseconds);

        // testing multiple fields with datetime
        result = db.HashFieldSetAndSetExpiry(hashKey, entries, DateTime.Now.AddMinutes(120));
        result.Should().Be(1);
        fieldTtls = db.HashFieldGetTimeToLive(hashKey, fields);
        ((double)fieldTtls[0]).Should().BeInRange(TimeSpan.FromMinutes(119).TotalMilliseconds, TimeSpan.FromHours(2).TotalMilliseconds);
        ((double)fieldTtls[1]).Should().BeInRange(TimeSpan.FromMinutes(119).TotalMilliseconds, TimeSpan.FromHours(2).TotalMilliseconds);

        // testing multiple fields with keepttl
        result = db.HashFieldSetAndSetExpiry(hashKey, entries, keepTtl: true);
        result.Should().Be(1);
        fieldTtls = db.HashFieldGetTimeToLive(hashKey, fields);
        ((double)fieldTtls[0]).Should().BeInRange(TimeSpan.FromMinutes(119).TotalMilliseconds, TimeSpan.FromHours(2).TotalMilliseconds);
        ((double)fieldTtls[1]).Should().BeInRange(TimeSpan.FromMinutes(119).TotalMilliseconds, TimeSpan.FromHours(2).TotalMilliseconds);

        // testing with ExpireWhen.Exists
        db.KeyDelete(hashKey);
        result = db.HashFieldSetAndSetExpiry(hashKey, "f1", 1, TimeSpan.FromHours(1), when: When.Exists);
        result.Should().Be(0); // should not set because it doesnt exist

        // testing with ExpireWhen.NotExists
        result = db.HashFieldSetAndSetExpiry(hashKey, "f1", 1, TimeSpan.FromHours(1), when: When.NotExists);
        result.Should().Be(1); // should set because it doesnt exist
        fieldTtl = db.HashFieldGetTimeToLive(hashKey, new RedisValue[] { "f1" })[0];
        ((double)fieldTtl).Should().BeInRange(TimeSpan.FromMinutes(59).TotalMilliseconds, TimeSpan.FromHours(1).TotalMilliseconds);

        // testing with ExpireWhen.GreaterThanCurrentExpiry
        result = db.HashFieldSetAndSetExpiry(hashKey, "f1", -1, keepTtl: true, when: When.Exists);
        result.Should().Be(1); // should set because it exists
        fieldTtl = db.HashFieldGetTimeToLive(hashKey, new RedisValue[] { "f1" })[0];
        ((double)fieldTtl).Should().BeInRange(TimeSpan.FromMinutes(59).TotalMilliseconds, TimeSpan.FromHours(1).TotalMilliseconds);
    }

    [Fact]
    public async Task hash_field_set_and_set_expiry_async()
    {
        await using var conn = Create(require: RedisFeatures.v8_0_0_M04);
        var db = conn.GetDatabase();
        var hashKey = Me();

        // testing with timespan
        var result = await db.HashFieldSetAndSetExpiryAsync(hashKey, "f1", 1, TimeSpan.FromHours(1));
        result.Should().Be(1);
        var fieldTtl = db.HashFieldGetTimeToLive(hashKey, new RedisValue[] { "f1" })[0];
        ((double)fieldTtl).Should().BeInRange(TimeSpan.FromMinutes(59).TotalMilliseconds, TimeSpan.FromHours(1).TotalMilliseconds);

        // testing with datetime
        result = await db.HashFieldSetAndSetExpiryAsync(hashKey, "f1", 1, DateTime.Now.AddMinutes(120));
        result.Should().Be(1);
        fieldTtl = db.HashFieldGetTimeToLive(hashKey, new RedisValue[] { "f1" })[0];
        ((double)fieldTtl).Should().BeInRange(TimeSpan.FromMinutes(119).TotalMilliseconds, TimeSpan.FromHours(2).TotalMilliseconds);

        // testing with keepttl
        result = await db.HashFieldSetAndSetExpiryAsync(hashKey, "f1", 1, keepTtl: true);
        result.Should().Be(1);
        fieldTtl = db.HashFieldGetTimeToLive(hashKey, new RedisValue[] { "f1" })[0];
        ((double)fieldTtl).Should().BeInRange(TimeSpan.FromMinutes(119).TotalMilliseconds, TimeSpan.FromHours(2).TotalMilliseconds);

        // testing multiple fields with timespan
        result = await db.HashFieldSetAndSetExpiryAsync(hashKey, entries, TimeSpan.FromHours(1));
        result.Should().Be(1);
        var fieldTtls = db.HashFieldGetTimeToLive(hashKey, fields);
        ((double)fieldTtls[0]).Should().BeInRange(TimeSpan.FromMinutes(59).TotalMilliseconds, TimeSpan.FromHours(1).TotalMilliseconds);
        ((double)fieldTtls[1]).Should().BeInRange(TimeSpan.FromMinutes(59).TotalMilliseconds, TimeSpan.FromHours(1).TotalMilliseconds);

        // testing multiple fields with datetime
        result = await db.HashFieldSetAndSetExpiryAsync(hashKey, entries, DateTime.Now.AddMinutes(120));
        result.Should().Be(1);
        fieldTtls = db.HashFieldGetTimeToLive(hashKey, fields);
        ((double)fieldTtls[0]).Should().BeInRange(TimeSpan.FromMinutes(119).TotalMilliseconds, TimeSpan.FromHours(2).TotalMilliseconds);
        ((double)fieldTtls[1]).Should().BeInRange(TimeSpan.FromMinutes(119).TotalMilliseconds, TimeSpan.FromHours(2).TotalMilliseconds);

        // testing multiple fields with keepttl
        result = await db.HashFieldSetAndSetExpiryAsync(hashKey, entries, keepTtl: true);
        result.Should().Be(1);
        fieldTtls = db.HashFieldGetTimeToLive(hashKey, fields);
        ((double)fieldTtls[0]).Should().BeInRange(TimeSpan.FromMinutes(119).TotalMilliseconds, TimeSpan.FromHours(2).TotalMilliseconds);
        ((double)fieldTtls[1]).Should().BeInRange(TimeSpan.FromMinutes(119).TotalMilliseconds, TimeSpan.FromHours(2).TotalMilliseconds);

        // testing with ExpireWhen.Exists
        db.KeyDelete(hashKey);
        result = await db.HashFieldSetAndSetExpiryAsync(hashKey, "f1", 1, TimeSpan.FromHours(1), when: When.Exists);
        result.Should().Be(0); // should not set because it doesnt exist

        // testing with ExpireWhen.NotExists
        result = await db.HashFieldSetAndSetExpiryAsync(hashKey, "f1", 1, TimeSpan.FromHours(1), when: When.NotExists);
        result.Should().Be(1); // should set because it doesnt exist
        fieldTtl = db.HashFieldGetTimeToLive(hashKey, new RedisValue[] { "f1" })[0];
        ((double)fieldTtl).Should().BeInRange(TimeSpan.FromMinutes(59).TotalMilliseconds, TimeSpan.FromHours(1).TotalMilliseconds);

        // testing with ExpireWhen.GreaterThanCurrentExpiry
        result = await db.HashFieldSetAndSetExpiryAsync(hashKey, "f1", -1, keepTtl: true, when: When.Exists);
        result.Should().Be(1); // should set because it exists
        fieldTtl = db.HashFieldGetTimeToLive(hashKey, new RedisValue[] { "f1" })[0];
        ((double)fieldTtl).Should().BeInRange(TimeSpan.FromMinutes(59).TotalMilliseconds, TimeSpan.FromHours(1).TotalMilliseconds);
    }
    [Fact]
    public void hash_field_get_and_delete()
    {
        using var conn = Create(require: RedisFeatures.v8_0_0_M04);
        var db = conn.GetDatabase();
        var hashKey = Me();

        // single field
        db.HashSet(hashKey, entries);
        var fieldResult = db.HashFieldGetAndDelete(hashKey, "f1");
        fieldResult.Should().Be(1);
        db.HashExists(hashKey, "f1").Should().BeFalse();

        // multiple fields
        db.HashSet(hashKey, entries);
        var fieldResults = db.HashFieldGetAndDelete(hashKey, fields);
        fieldResults.Should().Equal(values);
        db.HashExists(hashKey, "f1").Should().BeFalse();
        db.HashExists(hashKey, "f2").Should().BeFalse();
    }

    [Fact]
    public async Task hash_field_get_and_delete_async()
    {
        await using var conn = Create(require: RedisFeatures.v8_0_0_M04);
        var db = conn.GetDatabase();
        var hashKey = Me();

        // single field
        db.HashSet(hashKey, entries);
        var fieldResult = await db.HashFieldGetAndDeleteAsync(hashKey, "f1");
        fieldResult.Should().Be(1);
        db.HashExists(hashKey, "f1").Should().BeFalse();

        // multiple fields
        db.HashSet(hashKey, entries);
        var fieldResults = await db.HashFieldGetAndDeleteAsync(hashKey, fields);
        fieldResults.Should().Equal(values);
        db.HashExists(hashKey, "f1").Should().BeFalse();
        db.HashExists(hashKey, "f2").Should().BeFalse();
    }
}
