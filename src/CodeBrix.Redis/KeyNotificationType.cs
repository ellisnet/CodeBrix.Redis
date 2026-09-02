using System.Diagnostics.CodeAnalysis;
using CodeBrix.Redis.Respite;

namespace CodeBrix.Redis; //was previously: StackExchange.Redis;

/// <summary>
/// The type of keyspace or keyevent notification.
/// </summary>
[AsciiHash(nameof(KeyNotificationTypeMetadata))]
public enum KeyNotificationType
{
    // note: initially presented alphabetically, but: new values *must* be appended, not inserted
    // (to preserve values of existing elements)
    /// <summary>
    /// A notification whose event name this library does not recognise.
    /// </summary>
    [AsciiHash("")]
    Unknown = 0,
    /// <summary>
    /// The <c>append</c> event, raised by <c>APPEND</c>.
    /// </summary>
    [AsciiHash("append")]
    Append = 1,
    /// <summary>
    /// The <c>copy</c> event, raised by <c>COPY</c>.
    /// </summary>
    [AsciiHash("copy")]
    Copy = 2,
    /// <summary>
    /// The <c>del</c> event, raised when a key is deleted.
    /// </summary>
    [AsciiHash("del")]
    Del = 3,
    /// <summary>
    /// The <c>expire</c> event, raised when an expiry is set on a key.
    /// </summary>
    [AsciiHash("expire")]
    Expire = 4,
    /// <summary>
    /// The <c>hdel</c> event, raised by <c>HDEL</c>.
    /// </summary>
    [AsciiHash("hdel")]
    HDel = 5,
    /// <summary>
    /// The <c>hexpired</c> event, raised when one or more hash fields expire.
    /// </summary>
    [AsciiHash("hexpired")]
    HExpired = 6,
    /// <summary>
    /// The <c>hincrbyfloat</c> event, raised by <c>HINCRBYFLOAT</c>.
    /// </summary>
    [AsciiHash("hincrbyfloat")]
    HIncrByFloat = 7,
    /// <summary>
    /// The <c>hincrby</c> event, raised by <c>HINCRBY</c>.
    /// </summary>
    [AsciiHash("hincrby")]
    HIncrBy = 8,
    /// <summary>
    /// The <c>hpersist</c> event, raised by <c>HPERSIST</c>.
    /// </summary>
    [AsciiHash("hpersist")]
    HPersist = 9,
    /// <summary>
    /// The <c>hset</c> event, raised when one or more hash fields are set.
    /// </summary>
    [AsciiHash("hset")]
    HSet = 10,
    /// <summary>
    /// The <c>incrbyfloat</c> event, raised by <c>INCRBYFLOAT</c>.
    /// </summary>
    [AsciiHash("incrbyfloat")]
    IncrByFloat = 11,
    /// <summary>
    /// The <c>incrby</c> event, raised when a string is incremented or decremented as an integer.
    /// </summary>
    [AsciiHash("incrby")]
    IncrBy = 12,
    /// <summary>
    /// The <c>linsert</c> event, raised by <c>LINSERT</c>.
    /// </summary>
    [AsciiHash("linsert")]
    LInsert = 13,
    /// <summary>
    /// The <c>lpop</c> event, raised by <c>LPOP</c>.
    /// </summary>
    [AsciiHash("lpop")]
    LPop = 14,
    /// <summary>
    /// The <c>lpush</c> event, raised when elements are pushed onto the head of a list.
    /// </summary>
    [AsciiHash("lpush")]
    LPush = 15,
    /// <summary>
    /// The <c>lrem</c> event, raised by <c>LREM</c>.
    /// </summary>
    [AsciiHash("lrem")]
    LRem = 16,
    /// <summary>
    /// The <c>lset</c> event, raised by <c>LSET</c>.
    /// </summary>
    [AsciiHash("lset")]
    LSet = 17,
    /// <summary>
    /// The <c>ltrim</c> event, raised by <c>LTRIM</c>.
    /// </summary>
    [AsciiHash("ltrim")]
    LTrim = 18,
    /// <summary>
    /// The <c>move_from</c> event, raised on the source database by <c>MOVE</c>.
    /// </summary>
    [AsciiHash("move_from")]
    MoveFrom = 19,
    /// <summary>
    /// The <c>move_to</c> event, raised on the destination database by <c>MOVE</c>.
    /// </summary>
    [AsciiHash("move_to")]
    MoveTo = 20,
    /// <summary>
    /// The <c>persist</c> event, raised when the expiry is removed from a key.
    /// </summary>
    [AsciiHash("persist")]
    Persist = 21,
    /// <summary>
    /// The <c>rename_from</c> event, raised on the old key name by <c>RENAME</c>.
    /// </summary>
    [AsciiHash("rename_from")]
    RenameFrom = 22,
    /// <summary>
    /// The <c>rename_to</c> event, raised on the new key name by <c>RENAME</c>.
    /// </summary>
    [AsciiHash("rename_to")]
    RenameTo = 23,
    /// <summary>
    /// The <c>restore</c> event, raised by <c>RESTORE</c>.
    /// </summary>
    [AsciiHash("restore")]
    Restore = 24,
    /// <summary>
    /// The <c>rpop</c> event, raised by <c>RPOP</c>.
    /// </summary>
    [AsciiHash("rpop")]
    RPop = 25,
    /// <summary>
    /// The <c>rpush</c> event, raised when elements are pushed onto the tail of a list.
    /// </summary>
    [AsciiHash("rpush")]
    RPush = 26,
    /// <summary>
    /// The <c>sadd</c> event, raised by <c>SADD</c>.
    /// </summary>
    [AsciiHash("sadd")]
    SAdd = 27,
    /// <summary>
    /// The <c>set</c> event, raised when the value of a string key is set.
    /// </summary>
    [AsciiHash("set")]
    Set = 28,
    /// <summary>
    /// The <c>setrange</c> event, raised by <c>SETRANGE</c>.
    /// </summary>
    [AsciiHash("setrange")]
    SetRange = 29,
    /// <summary>
    /// The <c>sortstore</c> event, raised when <c>SORT</c> writes its result to a key.
    /// </summary>
    [AsciiHash("sortstore")]
    SortStore = 30,
    /// <summary>
    /// The <c>srem</c> event, raised by <c>SREM</c>.
    /// </summary>
    [AsciiHash("srem")]
    SRem = 31,
    /// <summary>
    /// The <c>spop</c> event, raised by <c>SPOP</c>.
    /// </summary>
    [AsciiHash("spop")]
    SPop = 32,
    /// <summary>
    /// The <c>xadd</c> event, raised by <c>XADD</c>.
    /// </summary>
    [AsciiHash("xadd")]
    XAdd = 33,
    /// <summary>
    /// The <c>xdel</c> event, raised by <c>XDEL</c>.
    /// </summary>
    [AsciiHash("xdel")]
    XDel = 34,
    /// <summary>
    /// The <c>xgroup-createconsumer</c> event, raised by <c>XGROUP CREATECONSUMER</c>.
    /// </summary>
    [AsciiHash("xgroup-createconsumer")]
    XGroupCreateConsumer = 35,
    /// <summary>
    /// The <c>xgroup-create</c> event, raised by <c>XGROUP CREATE</c>.
    /// </summary>
    [AsciiHash("xgroup-create")]
    XGroupCreate = 36,
    /// <summary>
    /// The <c>xgroup-delconsumer</c> event, raised by <c>XGROUP DELCONSUMER</c>.
    /// </summary>
    [AsciiHash("xgroup-delconsumer")]
    XGroupDelConsumer = 37,
    /// <summary>
    /// The <c>xgroup-destroy</c> event, raised by <c>XGROUP DESTROY</c>.
    /// </summary>
    [AsciiHash("xgroup-destroy")]
    XGroupDestroy = 38,
    /// <summary>
    /// The <c>xgroup-setid</c> event, raised by <c>XGROUP SETID</c>.
    /// </summary>
    [AsciiHash("xgroup-setid")]
    XGroupSetId = 39,
    /// <summary>
    /// The <c>xsetid</c> event, raised by <c>XSETID</c>.
    /// </summary>
    [AsciiHash("xsetid")]
    XSetId = 40,
    /// <summary>
    /// The <c>xtrim</c> event, raised by <c>XTRIM</c>.
    /// </summary>
    [AsciiHash("xtrim")]
    XTrim = 41,
    /// <summary>
    /// The <c>zadd</c> event, raised by <c>ZADD</c>.
    /// </summary>
    [AsciiHash("zadd")]
    ZAdd = 42,
    /// <summary>
    /// The <c>zdiffstore</c> event, raised by <c>ZDIFFSTORE</c>.
    /// </summary>
    [AsciiHash("zdiffstore")]
    ZDiffStore = 43,
    /// <summary>
    /// The <c>zinterstore</c> event, raised by <c>ZINTERSTORE</c>.
    /// </summary>
    [AsciiHash("zinterstore")]
    ZInterStore = 44,
    /// <summary>
    /// The <c>zunionstore</c> event, raised by <c>ZUNIONSTORE</c>.
    /// </summary>
    [AsciiHash("zunionstore")]
    ZUnionStore = 45,
    /// <summary>
    /// The <c>zincr</c> event, raised when the score of a sorted-set member is incremented.
    /// </summary>
    [AsciiHash("zincr")]
    ZIncr = 46,
    /// <summary>
    /// The <c>zrembyrank</c> event, raised by <c>ZREMRANGEBYRANK</c>.
    /// </summary>
    [AsciiHash("zrembyrank")]
    ZRemByRank = 47,
    /// <summary>
    /// The <c>zrembyscore</c> event, raised by <c>ZREMRANGEBYSCORE</c>.
    /// </summary>
    [AsciiHash("zrembyscore")]
    ZRemByScore = 48,
    /// <summary>
    /// The <c>zrem</c> event, raised by <c>ZREM</c>.
    /// </summary>
    [AsciiHash("zrem")]
    ZRem = 49,
    /// <summary>
    /// The <c>hexpire</c> event, raised when an expiry is set on one or more hash fields.
    /// </summary>
    [AsciiHash("hexpire")]
    HExpire = 50,
    /// <summary>
    /// The <c>ardel</c> event, raised by <c>ARDEL</c>.
    /// </summary>
    [AsciiHash("ardel")]
    ArDel = 51,
    /// <summary>
    /// The <c>ardelrange</c> event, raised by <c>ARDELRANGE</c>.
    /// </summary>
    [AsciiHash("ardelrange")]
    ArDelRange = 52,
    /// <summary>
    /// The <c>arset</c> event, raised by <c>ARSET</c>.
    /// </summary>
    [AsciiHash("arset")]
    ArSet = 53,

    // side-effect notifications
    /// <summary>
    /// The <c>expired</c> event, raised when a key is removed because its time to live elapsed.
    /// </summary>
    [AsciiHash("expired")]
    Expired = 1000,
    /// <summary>
    /// The <c>evicted</c> event, raised when a key is removed to stay within <c>maxmemory</c>.
    /// </summary>
    [AsciiHash("evicted")]
    Evicted = 1001,
    /// <summary>
    /// The <c>new</c> event, raised when a key that did not exist is created.
    /// </summary>
    [AsciiHash("new")]
    New = 1002,
    /// <summary>
    /// The <c>overwritten</c> event, raised when an existing key's value is replaced.
    /// </summary>
    [AsciiHash("overwritten")]
    Overwritten = 1003,
    /// <summary>
    /// The <c>type_changed</c> event, raised when a key's value changes to a different type.
    /// </summary>
    [AsciiHash("type_changed")]
    TypeChanged = 1004,
}
