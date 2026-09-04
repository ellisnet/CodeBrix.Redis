================================================================================
AGENT-README: CodeBrix.Redis
A Guide for AI Coding Agents — CONSUMING the CodeBrix.Redis.MitLicenseForever
NuGet package
================================================================================

OVERVIEW
========
CodeBrix.Redis is a high-performance, fully managed Redis client for .NET,
covering both synchronous and asynchronous usage. It is one assembly holding the
whole stack:

  * a RESP protocol reader and its buffers, usable on their own;
  * a connection multiplexer that owns the sockets, pipelines commands, and
    keeps track of which server in a topology owns which key;
  * the command surface - strings, hashes, lists, sets, sorted sets, bitmaps,
    HyperLogLog, geospatial indexes, streams and vector sets;
  * publish/subscribe, transactions, batches and Lua scripting;
  * the Redlock distributed-lock algorithm.

It is a PORT, and that is the most useful thing to know about it. It replaces
three MIT-licensed packages, and its types, members, signatures, nullability
annotations and behaviour are theirs:

    StackExchange.Redis   ->  namespace CodeBrix.Redis
    RESPite               ->  namespace CodeBrix.Redis.Respite
    RedLock.net           ->  namespace CodeBrix.Redis.RedLock

The practical consequence for you: everything you know about StackExchange.Redis
applies here unchanged, and so does everything written about it anywhere else.
The ONLY change when migrating is the using directives. If a StackExchange.Redis
answer says to call db.StringSetAsync(key, value), call db.StringSetAsync(key,
value). Do not go looking for a CodeBrix-flavoured alternative name; there isn't
one, on purpose.

Target framework: .NET 10 or later. License: MIT.

    your code ─► ConnectionMultiplexer ─► IDatabase / ISubscriber / IServer
                        │
                        ├─ one connection per server in the topology,
                        │       each with its own bridge down to a socket
                        └─ the RESP reader and its buffers

    Only the first line is API. Everything under the multiplexer is internal
    machinery you do not name, do not construct and do not need - except the RESP
    layer, which is public in its own right under CodeBrix.Redis.Respite.

ONE MULTIPLEXER PER APPLICATION. This is the single most important thing about
using this library correctly, and it is the mistake most often made with it. See
COMMON PITFALLS TO AVOID.


INSTALLATION
============
    dotnet add package CodeBrix.Redis.MitLicenseForever

The package id carries the license suffix; the namespaces do not. You install
CodeBrix.Redis.MitLicenseForever and you write "using CodeBrix.Redis;".

The package brings two dependencies, both published by Microsoft:

    Microsoft.Extensions.Logging.Abstractions
    System.IO.Hashing

The first is there because ILoggerFactory is part of the public surface, not
merely internal plumbing - see ConfigurationOptions.LoggerFactory. If your
application already uses Microsoft.Extensions.Logging, your existing
ILoggerFactory is the one to hand over.

The package also carries a Roslyn payload under analyzers/dotnet/cs, which
installs itself when you reference the package: two analyzers - the transaction
analyzer and the queued-result analyzer - and two incremental source generators.
The analyzers report the same SERxxxx diagnostics the upstream
StackExchange.Redis package reports, with code fixes in the IDE. Of the two
generators, the auto-database generator checks the assembly name and does
nothing at all outside this library, while the ASCII-hash generator will emit
source in your compilation if you use the [AsciiHash] attribute - which is
behind an opt-in gate, so ordinary client code never meets it. All four are
compile-time only and nothing about them reaches your output. See THE SHIPPED
ANALYZERS for the rules and for the one MSBuild property that tunes them.

Requires a Redis server to talk to. The client negotiates capabilities with
each server it connects to and exposes what it found as RedisFeatures, reachable
from IServer.Features; a command the connected server cannot run reports a clear
error rather than silently misbehaving. Nothing has to be configured for this.
The one place a version is worth stating is the MSBuild property
<RedisMinServerVersion> - see THE SHIPPED ANALYZERS below.


KEY NAMESPACES / USINGS
=======================
    using CodeBrix.Redis;
        The client. ConnectionMultiplexer, IDatabase, ISubscriber, IServer,
        ITransaction, IBatch, RedisKey, RedisValue, RedisResult,
        ConfigurationOptions, RedisChannel, LuaScript, Condition, and the
        exception types. This is the only using most applications need.

    using CodeBrix.Redis.Configuration;
        DefaultOptionsProvider and its derivatives, for centralising connection
        defaults across an application or for an environment (Azure) whose
        conventions differ. Tunnel and LoggingTunnel live here too.

    using CodeBrix.Redis.Profiling;
        ProfilingSession, IProfiledCommand - per-command timing.

    using CodeBrix.Redis.KeyspaceIsolation;
        The WithKeyPrefix extension, which returns an IDatabase view that
        transparently prefixes every key.

    using CodeBrix.Redis.Maintenance;
        Server-maintenance notifications (failover, node migration) as events.

    using CodeBrix.Redis.RedLock;
        The distributed-lock algorithm: RedLockFactory, IRedLock, RedLockStatus,
        IDistributedLockFactory.

    using CodeBrix.Redis.RedLock.Configuration;
        What you HAND the factory: RedLockMultiplexer, RedLockEndPoint,
        RedLockRetryConfiguration, RedLockConfiguration. Taking a lock needs both
        this using and the one above - the split is upstream RedLock.net's own,
        preserved here.

    using CodeBrix.Redis.Availability;
        Multi-group connections, health checks and circuit breakers, for an
        application spanning several independent Redis deployments. Every type
        here is gated - see THE EXPERIMENTAL API GATES below.

    using CodeBrix.Redis.Respite;
        The low-level RESP layer. You want this only if you are speaking RESP to
        something that is not Redis, or writing a custom transport. The client
        does not require you to know it exists. The reader itself is
        CodeBrix.Redis.Respite.Messages.RespReader and the transports are in
        CodeBrix.Redis.Respite.Transports; all of it is gated - see THE
        EXPERIMENTAL API GATES below.


CORE API REFERENCE
==================

CONNECTING
----------
ConnectionMultiplexer is the entry point and the expensive object. Create one,
keep it, share it.

    static ConnectionMultiplexer Connect(string configuration, TextWriter log = null)
    static ConnectionMultiplexer Connect(ConfigurationOptions configuration, TextWriter log = null)
    static ConnectionMultiplexer Connect(string configuration, Action<ConfigurationOptions> configure, TextWriter log = null)
    static Task<ConnectionMultiplexer> ConnectAsync(...)      // same three shapes

The configuration string is a comma-separated list of endpoints followed by
comma-separated options:

    "localhost:6379"
    "redis0:6380,redis1:6380,allowAdmin=true"
    "cache.example.com:6380,password=...,ssl=true,abortConnect=false"

The equivalent object form is ConfigurationOptions, which is what the string
parses into. Use ConfigurationOptions.Parse to inspect a string, and
ToString()/ToString(includePassword) to render one back. Options worth knowing:

    EndPoints                 the servers; add with .Add(host, port)
    ClientName                shows up in CLIENT LIST - set it, always;
                              unset, the connection names itself
                              "<machine>(CodeBrix.Redis-v<version>)"
    User / Password           ACL credentials
    Ssl / SslHost             TLS, and the host name to validate against
    AbortOnConnectFail        false to keep retrying instead of throwing at
                              startup; usually what you want in a service
    ConnectTimeout            milliseconds to establish a connection
    ConnectRetry              connect attempts before giving up
    SyncTimeout               milliseconds a synchronous call may take
    AsyncTimeout              the same for asynchronous calls
    DefaultDatabase           the database GetDatabase() means by default
    AllowAdmin                unlocks the destructive IServer commands
    CommandMap                disable or rename commands (for proxies)
    Protocol                  RESP2 or RESP3
    LoggerFactory             an ILoggerFactory for connection-level logging
    ReconnectRetryPolicy      ExponentialRetry or LinearRetry
    BacklogPolicy             what to do with commands issued while the
                              connection is down
    ServiceName               the sentinel service name, when connecting
                              through sentinels

Instance members:

    IDatabase GetDatabase(int db = -1, object asyncState = null)
    ISubscriber GetSubscriber(object asyncState = null)
    IServer GetServer(string hostAndPort, object asyncState = null)
    IServer GetServer(string host, int port, object asyncState = null)
    IServer GetServer(EndPoint endpoint, object asyncState = null)
    IServer[] GetServers()
    string GetStatus()
    void Close(bool allowCommandsToComplete = true)
    Task CloseAsync(bool allowCommandsToComplete = true)
    void Dispose()
    ValueTask DisposeAsync()
    bool IsConnected { get; }
    string ClientName { get; }
    string Configuration { get; }

GetDatabase and GetSubscriber are CHEAP. They return lightweight facades over
the same multiplexer, not new connections - call them freely rather than caching
the result in a field for performance reasons. GetServer is likewise cheap but
is a different kind of object: it targets ONE specific server, which is what you
want for server-wide commands and what you must not use for ordinary data
access.

Events, all on the multiplexer:

    ConnectionFailed / ConnectionRestored     with the failure type and endpoint
    ErrorMessage                              a server-reported error
    InternalError                             an unexpected client-side fault
    HashSlotMoved                             cluster slot migration observed
    ConfigurationChanged / ConfigurationChangedBroadcast
    ServerMaintenanceEvent                    scheduled maintenance notice

Subscribe to ConnectionFailed and ConnectionRestored and log them. When
something is wrong in production these two are the difference between a
diagnosis and a guess.


THE DATA API - IDatabase
------------------------
IDatabase is the command surface. Every method exists in a synchronous form and
an Async form returning Task; IDatabaseAsync holds the asynchronous half. Prefer
the asynchronous form throughout.

Every method takes a trailing optional CommandFlags parameter:

    CommandFlags.None                 the default
    CommandFlags.FireAndForget        do not wait for or read the reply
    CommandFlags.PreferReplica        read from a replica when one is available
    CommandFlags.DemandReplica        require a replica
    CommandFlags.PreferMaster / DemandMaster
    CommandFlags.NoRedirect           do not follow MOVED/ASK
    CommandFlags.NoScriptCache        do not use EVALSHA

The command families, by area. Names follow the Redis command they issue, so
StringGetAsync is GET, HashSetAsync is HSET, and so on:

    Keys           KeyExists, KeyDelete, KeyExpire, KeyTimeToLive, KeyRename,
                   KeyType, KeyPersist, KeyDump/KeyRestore, KeyRandom,
                   KeyTouch, KeyCopy, KeyMove
    Strings        StringGet, StringSet, StringSetAndGet, StringAppend,
                   StringIncrement, StringDecrement, StringLength,
                   StringGetRange, StringSetRange, StringGetDelete,
                   StringGetSetExpiry, StringGetWithExpiry, StringGetLease,
                   StringBitCount, StringBitOperation, StringBitPosition,
                   StringLongestCommonSubsequence
    Hashes         HashGet, HashGetAll, HashSet, HashDelete, HashExists,
                   HashIncrement, HashDecrement, HashKeys, HashValues,
                   HashLength, HashRandomField, HashScan, HashStringLength,
                   plus the field-expiry family (HashFieldExpire,
                   HashFieldGetTimeToLive, HashFieldPersist)
    Lists          ListLeftPush, ListRightPush, ListLeftPop, ListRightPop,
                   ListRange, ListLength, ListRemove, ListInsertBefore/After,
                   ListSetByIndex, ListTrim, ListMove, ListPosition,
                   ListRightPopLeftPush
    Sets           SetAdd, SetRemove, SetMembers, SetContains, SetLength,
                   SetPop, SetRandomMember(s), SetMove, SetScan,
                   SetCombine (union/intersect/difference),
                   SetCombineAndStore, SetIntersectionLength
    Sorted sets    SortedSetAdd, SortedSetRemove, SortedSetScore,
                   SortedSetIncrement, SortedSetLength, SortedSetRank,
                   SortedSetRangeByRank/ByScore/ByValue and the
                   ...WithScores variants, SortedSetRangeAndStore,
                   SortedSetPop, SortedSetRandomMember(s), SortedSetScan,
                   SortedSetCombine and its ...AndStore/...WithScores forms,
                   SortedSetRemoveRangeBy*
    Streams        StreamAdd, StreamRead, StreamReadGroup, StreamRange,
                   StreamAcknowledge, StreamAutoClaim, StreamClaim,
                   StreamCreateConsumerGroup, StreamConsumerGroupSetPosition,
                   StreamDelete, StreamDeleteConsumer,
                   StreamDeleteConsumerGroup, StreamGroupInfo, StreamInfo,
                   StreamLength, StreamPending, StreamPendingMessages,
                   StreamTrim
    Vector sets    VectorSetAdd, VectorSetLength, VectorSetDimension,
                   VectorSetGetApproximateVector, VectorSetGetAttributesJson,
                   VectorSetSetAttributesJson, VectorSetGetLinks,
                   VectorSetContains, VectorSetRemove, VectorSetInfo,
                   VectorSetRandomMember(s), VectorSetSimilaritySearch
    HyperLogLog    HyperLogLogAdd, HyperLogLogLength, HyperLogLogMerge
    Geospatial     GeoAdd, GeoRemove, GeoDistance, GeoHash, GeoPosition,
                   GeoSearch, GeoSearchAndStore
    Sorting        Sort, SortAndStore
    Scripting      ScriptEvaluate, ScriptEvaluateReadOnly (see SCRIPTING)
    Transport      Execute / ExecuteAsync for an arbitrary command,
                   Ping, IdentifyEndpoint, IsConnected

RedisKey and RedisValue are structs, and they convert, so you rarely name them.
The direction matters:

  * INTO them is implicit. A RedisKey takes a string or a byte[]; a RedisValue
    takes those plus bool, int, long, uint, ulong, double, their nullable forms,
    and Memory<byte> / ReadOnlyMemory<byte> / ReadOnlySequence<byte>.
  * OUT of them, string and the byte-buffer forms are implicit, and every
    numeric and bool form is an EXPLICIT cast - which is what makes the
    conversion visible where it can fail.

    await db.StringSetAsync("key", 42);
    long n = (long)await db.StringGetAsync("key");

A RedisValue that is missing is not null; it is RedisValue.Null, and
value.IsNull and value.IsNullOrEmpty test it. Casting a missing value to a
non-nullable numeric type throws; cast to a nullable type (long?) instead, or
check IsNull first.

RedisResult is what an arbitrary command or a script returns - a discriminated
shape you interrogate and cast from. Read its shape with .Resp2Type (the
simplified view, and what you want unless you are handling RESP3 replies
specifically) or .Resp3Type (the full one). The plain .Type property is
[Obsolete] and says the same thing.


PUBLISH/SUBSCRIBE - ISubscriber
-------------------------------
    ISubscriber sub = multiplexer.GetSubscriber();

    void Subscribe(RedisChannel channel, Action<RedisChannel, RedisValue> handler, CommandFlags flags = None)
    Task SubscribeAsync(RedisChannel channel, Action<RedisChannel, RedisValue> handler, CommandFlags flags = None)
    ChannelMessageQueue Subscribe(RedisChannel channel, CommandFlags flags = None)
    Task<ChannelMessageQueue> SubscribeAsync(RedisChannel channel, CommandFlags flags = None)
    long Publish(RedisChannel channel, RedisValue message, CommandFlags flags = None)
    Task<long> PublishAsync(...)
    void Unsubscribe(...) / UnsubscribeAll(...)

RedisChannel is explicit about pattern matching, and you should be too:

    RedisChannel.Literal("news")        exactly this channel
    RedisChannel.Pattern("news.*")      a glob pattern

The two Subscribe shapes differ in ordering, and the difference matters. The
handler overload may invoke your handler CONCURRENTLY for different messages.
ChannelMessageQueue delivers messages strictly in order, one at a time:

    ChannelMessageQueue queue = await sub.SubscribeAsync(RedisChannel.Literal("news"));
    queue.OnMessage(message => Handle(message.Message));

    // or pull, rather than push:
    while (true)
    {
        ChannelMessage message = await queue.ReadAsync(cancellationToken);
        Handle(message.Message);
    }

If order matters at all, use the queue.


TRANSACTIONS AND BATCHES
------------------------
A BATCH is a pipelining device: the commands are sent together to reduce
round-trips, and that is all. They are not atomic and nothing prevents other
clients interleaving.

    IBatch batch = db.CreateBatch();
    Task<bool> a = batch.StringSetAsync("k1", "v1");
    Task<bool> b = batch.StringSetAsync("k2", "v2");
    batch.Execute();
    await Task.WhenAll(a, b);

A TRANSACTION is MULTI/EXEC with preconditions. Conditions are checked with
WATCH before the transaction runs; if any fails, nothing executes and Execute
returns false.

    ITransaction tran = db.CreateTransaction();
    tran.AddCondition(Condition.StringEqual("owner", "me"));
    Task<bool> set = tran.StringSetAsync("state", "claimed");
    bool committed = await tran.ExecuteAsync();

The trap is universal and worth stating plainly: DO NOT await the individual
command tasks before calling Execute. They do not complete until the transaction
executes, so awaiting one first deadlocks. Capture them, call Execute, then
await. The shipped analyzer catches this, as SER305.

Condition offers the full family: KeyExists, KeyNotExists, StringEqual,
StringNotEqual, HashExists, HashNotExists, HashEqual, ListIndexEqual,
SetContains, SortedSetContains, and the length comparisons.


SCRIPTING
---------
Raw, with numbered KEYS and ARGV:

    RedisResult result = await db.ScriptEvaluateAsync(
        "return redis.call('set', KEYS[1], ARGV[1])",
        [(RedisKey)"key"],
        [(RedisValue)"value"]);

Or with named parameters, which is easier to read and much easier to get right:

    LuaScript script = LuaScript.Prepare("return redis.call('set', @key, @value)");
    RedisResult result = await script.EvaluateAsync(db, new { key = (RedisKey)"key", value = "value" });

LuaScript rewrites @name into the correct KEYS/ARGV slot based on the parameter
type - a RedisKey becomes a KEY, everything else an ARGV - which is what makes
scripts cluster-safe. Load it once per server with Load/LoadAsync to get a
LoadedLuaScript that evaluates by hash.


SERVER OPERATIONS - IServer
---------------------------
IServer targets one specific server. It carries the administrative and
introspective commands: Keys (a cursored SCAN, not KEYS), FlushDatabase,
FlushAllDatabases, Info, ConfigGet, ConfigSet, ClientList, ClientKill,
DatabaseSize, Time, LastSave, Save (which takes a SaveType - ForegroundSave or
BackgroundSave), SlowlogGet, ScriptExists, ScriptLoad, ScriptFlush, MemoryStats,
Execute for an arbitrary command, the replication commands (ReplicaOfAsync,
MakePrimaryAsync), the cluster commands (ClusterNodes, ClusterConfiguration),
and the sentinel commands (SentinelGetMasterAddressByName,
SentinelGetReplicaAddresses, SentinelFailover, and their Async forms).
IServer.Features is the negotiated capability set for that server.

Call the replication members in their Async form. The synchronous ReplicaOf and
MakeMaster are [Obsolete] as errors, exactly as upstream marks them, so naming
either one fails the build; write ReplicaOfAsync and MakePrimaryAsync instead.
SlaveOf and SlaveOfAsync are obsolete as errors on the same terms, from the move
to "replica" terminology - ReplicaOfAsync is what replaces both of them.

Destructive members require AllowAdmin=true in the configuration. That is a
deliberate speed bump; leave it off in application configuration and set it only
in the tool that needs it.

server.Keys() is a cursored enumeration and safe on a large keyspace. Never
issue a raw KEYS command against production.


KEY-SPACE ISOLATION
-------------------
    using CodeBrix.Redis.KeyspaceIsolation;

    IDatabase tenant = db.WithKeyPrefix("tenant:42:");
    await tenant.StringSetAsync("state", "ok");   // writes tenant:42:state

The returned IDatabase is a view: every key going out is prefixed and the
prefixing is invisible to your code. Useful for multi-tenanting one Redis
database, and for keeping test data separable.


DISTRIBUTED LOCKS
-----------------
    using CodeBrix.Redis.RedLock;
    using CodeBrix.Redis.RedLock.Configuration;   // RedLockMultiplexer lives here

    RedLockFactory factory = RedLockFactory.Create(
        [new RedLockMultiplexer(multiplexer)], loggerFactory);

    await using IRedLock redLock = await factory.CreateLockAsync(
        resource: "order-42",
        expiryTime: TimeSpan.FromSeconds(30));

    if (redLock.IsAcquired) { /* exclusive work */ }

The factory is created once and shared, like the multiplexer. Create it either
from existing multiplexers (RedLockMultiplexer) or from endpoints
(RedLockEndPoint), optionally with a RedLockRetryConfiguration.

The blocking overload waits and retries:

    await using IRedLock redLock = await factory.CreateLockAsync(
        "order-42",
        expiryTime: TimeSpan.FromSeconds(30),
        waitTime: TimeSpan.FromSeconds(10),
        retryTime: TimeSpan.FromSeconds(1),
        cancellationToken);

ALWAYS check IsAcquired. A failed lock is not an exception - it is an IRedLock
whose IsAcquired is false and whose Status says why (RedLockStatus.Conflicted,
NoQuorum, and so on). Disposing releases the lock; the lock also auto-extends
while held, and expiryTime is the ceiling if your process dies.

For real mutual exclusion the algorithm needs a quorum of INDEPENDENT Redis
instances - not one instance, and not a primary with its replicas. A single
instance gives you a convenient lock, not a safe one.


PROFILING
---------
    using CodeBrix.Redis.Profiling;

    var session = new ProfilingSession();
    multiplexer.RegisterProfiler(() => session);
    // ... work ...
    IEnumerable<IProfiledCommand> commands = session.FinishProfiling();

Each IProfiledCommand carries the command, the endpoint, the database, the
retransmission reason if any, and the elapsed time broken into creation,
enqueue, send, response and completion. This is the tool for "why is this slow"
questions; it is far more informative than wrapping calls in a Stopwatch.


THE ERROR MODEL
---------------
    RedisConnectionException      could not connect, or the connection died.
                                  Carries a ConnectionFailureType.
    RedisTimeoutException         the command did not complete within
                                  SyncTimeout/AsyncTimeout. The message includes
                                  queue depths and is worth logging whole.
    RedisServerException          the server returned an error reply.
    RedisCommandException         the command is not valid - wrong argument
                                  count, disabled in the CommandMap, or needs a
                                  server feature that is not present.
    RedisException                the base of RedisConnectionException and
                                  RedisServerException, and of those two ONLY.

Read that last line carefully before writing a catch clause. The hierarchy is
upstream's and it is not what the names suggest: RedisCommandException derives
straight from Exception, and RedisTimeoutException derives from
TimeoutException, so "catch (RedisException)" catches neither of them. To
handle all four, catch the specific types you mean:

    try
    {
        await db.StringSetAsync("k", "v");
    }
    catch (RedisTimeoutException) { /* client or server was too slow */ }
    catch (RedisConnectionException) { /* no usable connection */ }
    catch (RedisServerException) { /* the server replied with an error */ }
    catch (RedisCommandException) { /* the command itself was not valid */ }

A RedisTimeoutException message is a diagnostic report, not a sentence. It names
the inbound and outbound queue sizes, the busy/min worker and IO thread counts,
and the local and server endpoints. Log the entire message; the answer is
usually in it.


THE SHIPPED ANALYZERS
---------------------
Referencing the package installs Roslyn analyzers into your compilation. You do
not configure anything and nothing reaches your output; they read your code and
report SERxxxx diagnostics - warnings, except SER305, which is an error because
the code it names can only ever deadlock - with code fixes in the IDE. They are
the upstream StackExchange.Redis rules, on the upstream identifiers, so a project
migrating onto this package keeps whatever NoWarn entries, .editorconfig
severities and inline suppressions it already had.

The rules in the 300 range are the ones a consumer meets:

    SER300  a transaction whose condition duplicates an argument the command
            already has - Condition.KeyNotExists guarding StringSet on the same
            key IS StringSet(key, value, When.NotExists)
    SER301  a transaction that a single newer atomic command subsumes
    SER302  a transaction condition that is redundant
    SER303  two queued operations that are one compound command
    SER304  repeated queued calls that suit the variadic overload
    SER305  awaiting a queued command's task before Execute[Async]() - it never
            completes; this is the deadlock described under TRANSACTIONS, and it
            is the one rule here reported as an ERROR
    SER306  awaiting the result of a fire-and-forget call, which is always the
            default value
    SER307  blocking on a redis call instead of awaiting it
    SER308  blocking on a task through the library's Wait helpers
    SER350  the generated code needs a newer C# language version than the
            project is compiling with, so nothing was generated - raise
            <LangVersion>. Reported by the ASCII-hash generator, so you meet it
            only if you have opted in to [AsciiHash] and used it

SER301 and its relatives depend on the server you will run against, which an
analyzer cannot see, so they are reported by default. Declare the floor and you
are only told about commands your server can actually run:

    <PropertyGroup>
      <RedisMinServerVersion>7.4</RedisMinServerVersion>
    </PropertyGroup>

The property name is the upstream one, so an existing setting keeps working
after the swap. The .editorconfig / .globalconfig spelling is
redis.min_server_version, and it takes precedence. Major.minor is what is read.

One thing to know about your tooling: this Roslyn payload is compiled against
the compiler of a current .NET 10 SDK, and an analyzer cannot load in a compiler
or IDE older than the one it was built against. Build with a current .NET 10
SDK. On older tooling the analyzer simply loads nothing and its diagnostics are
silently absent - the build still succeeds, so nothing tells you the checks
stopped running.


THE EXPERIMENTAL API GATES
--------------------------
Some of this library's public types are marked [Experimental], exactly as
upstream marks them. C# raises an [Experimental] diagnostic as a compile ERROR,
so NAMING one of those types stops your build until you opt in - which is the
point of the attribute, not a defect. The gates, and what is behind each:

    SER004  the whole CodeBrix.Redis.Respite protocol layer - RespReader,
            RespPrefix, RespException, the buffers, AsciiHash
    SER005  TestHarness, the unit-testing support type
    SER007  the whole CodeBrix.Redis.Availability namespace - connection groups,
            health checks, circuit breakers - plus the retry and
            circuit-breaker members that sit outside it: the RedisErrorKind
            enum, ConfigurationOptions.CircuitBreaker and .RetryPolicy, the
            eight CommandFlags.CommandRetry* values,
            ConnectionFailureType.CircuitBreaker, and
            IDatabaseAsync.CreateTransaction
    SER008  HashImport
    SER009  the transport surface - TlsOptions,
            CodeBrix.Redis.Respite.Transports

Ordinary client code - ConnectionMultiplexer, IDatabase, ISubscriber, IServer,
transactions, batches, scripting, pub/sub, key prefixing and the Redlock
algorithm - touches none of them and needs no opt-in at all. Nothing in this
document outside this section names a gated type.

If you do reach for one, opt in the way the language provides, in YOUR project,
exactly as you would with the upstream package:

    <PropertyGroup>
      <NoWarn>$(NoWarn);SER001;SER004;SER005;SER007;SER008;SER009</NoWarn>
    </PropertyGroup>

or mark the consuming member [Experimental] itself. An identifier that is not
listed stays gated, which is what you want: opt in to the gate you meant.

SER001 appears on that line for parity with the upstream project's own opt-in,
and this package declares no gate under it - the gates it actually declares are
the five listed above. Carrying it costs nothing and lets a project that already
had upstream's line paste it across unchanged.


COMPLETE EXAMPLES
=================

REGISTERING THE MULTIPLEXER IN A HOSTED APPLICATION
---------------------------------------------------
    using CodeBrix.Redis;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
    {
        var options = ConfigurationOptions.Parse(
            builder.Configuration.GetConnectionString("Redis"));

        options.ClientName = "orders-api";
        options.AbortOnConnectFail = false;
        options.LoggerFactory = provider.GetRequiredService<ILoggerFactory>();

        return ConnectionMultiplexer.Connect(options);
    });

    builder.Services.AddScoped(provider =>
        provider.GetRequiredService<IConnectionMultiplexer>().GetDatabase());

Singleton for the multiplexer, scoped or transient for the IDatabase. Registering
the multiplexer as anything other than a singleton is the pitfall below.


A CACHE-ASIDE READ
------------------
    using CodeBrix.Redis;
    using System.Text.Json;

    public sealed class OrderCache(IDatabase db)
    {
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

        public async Task<Order> GetAsync(int id, Func<Task<Order>> load)
        {
            RedisKey key = $"order:{id}";

            RedisValue cached = await db.StringGetAsync(key);
            if (!cached.IsNull)
            {
                return JsonSerializer.Deserialize<Order>((string)cached);
            }

            Order order = await load();

            await db.StringSetAsync(key, JsonSerializer.Serialize(order), Ttl);

            return order;
        }
    }


A WORK QUEUE ON A STREAM, WITH A CONSUMER GROUP
-----------------------------------------------
    using CodeBrix.Redis;

    const string Stream = "jobs";
    const string Group = "workers";

    // once, at startup - createStream:true so it works on an absent key
    if (!await db.KeyExistsAsync(Stream) ||
        (await db.StreamGroupInfoAsync(Stream)).All(g => g.Name != Group))
    {
        await db.StreamCreateConsumerGroupAsync(Stream, Group, StreamPosition.NewMessages, createStream: true);
    }

    // producing
    await db.StreamAddAsync(Stream, "payload", "{ \"id\": 42 }");

    // consuming
    StreamEntry[] entries = await db.StreamReadGroupAsync(
        Stream, Group, consumerName: Environment.MachineName, count: 10);

    foreach (StreamEntry entry in entries)
    {
        await Process(entry);
        await db.StreamAcknowledgeAsync(Stream, Group, entry.Id);
    }


AN ATOMIC CLAIM, WITH A PRECONDITION
------------------------------------
    using CodeBrix.Redis;

    ITransaction tran = db.CreateTransaction();

    tran.AddCondition(Condition.KeyNotExists("job:42:owner"));

    Task setOwner = tran.StringSetAsync("job:42:owner", workerId);
    Task setState = tran.StringSetAsync("job:42:state", "running");

    if (await tran.ExecuteAsync())
    {
        await Task.WhenAll(setOwner, setState);   // AFTER Execute, never before
    }
    else
    {
        // somebody else claimed it
    }


ORDERED PUB/SUB CONSUMPTION
---------------------------
    using CodeBrix.Redis;

    ISubscriber sub = multiplexer.GetSubscriber();

    ChannelMessageQueue queue = await sub.SubscribeAsync(RedisChannel.Literal("prices"));

    while (!cancellationToken.IsCancellationRequested)
    {
        ChannelMessage message = await queue.ReadAsync(cancellationToken);
        await Apply(message.Message);
    }

    await queue.UnsubscribeAsync();


SCANNING A LARGE KEYSPACE SAFELY
--------------------------------
    using CodeBrix.Redis;

    foreach (IServer server in multiplexer.GetServers())
    {
        if (server.IsReplica || !server.IsConnected)
        {
            continue;
        }

        foreach (RedisKey key in server.Keys(pattern: "session:*", pageSize: 1000))
        {
            await db.KeyDeleteAsync(key);
        }
    }

server.Keys issues SCAN under the covers and pages. Iterate servers explicitly,
because in a cluster each server holds a different part of the keyspace.


MINIMUM VIABLE PROJECT TEMPLATE
===============================
    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net10.0</TargetFramework>
      </PropertyGroup>
      <ItemGroup>
        <PackageReference Include="CodeBrix.Redis.MitLicenseForever" Version="*" />
      </ItemGroup>
    </Project>

    // Program.cs
    using System;
    using CodeBrix.Redis;

    await using var multiplexer = await ConnectionMultiplexer.ConnectAsync("localhost:6379");

    IDatabase db = multiplexer.GetDatabase();

    await db.StringSetAsync("greeting", "hello world");

    Console.WriteLine(await db.StringGetAsync("greeting"));

Pin a real version rather than "*" in anything you intend to keep.


PERFORMANCE TIPS
================
  * One multiplexer, application-wide. It multiplexes: thousands of concurrent
    operations share one connection per server, and that is the design working,
    not a bottleneck.

  * Prefer the Async methods everywhere. The synchronous ones block a thread
    waiting on a reply that arrives on another; under load that is where
    thread-pool starvation comes from, and thread-pool starvation is the usual
    cause of a RedisTimeoutException on an otherwise healthy server.

  * Batch when you have several independent commands. CreateBatch turns N
    round-trips into one. If you do not need the replies at all, add
    CommandFlags.FireAndForget and do not even wait for them.

  * Do not open a transaction for commands that do not need atomicity. MULTI/EXEC
    costs more than a batch and buys nothing if there is no precondition.

  * Keep values small. Redis is single-threaded per server: one multi-megabyte
    value blocks every other client for as long as it takes to move.

  * Set ClientName. It costs nothing and turns CLIENT LIST from a wall of
    anonymous sockets into something you can read during an incident.

  * Use PreferReplica for read-heavy work against a replicated topology, but
    only where a slightly stale read is acceptable - replication is
    asynchronous.

  * Reuse LuaScript objects and load them once per server. Preparing a script
    parses it; EVALSHA against a loaded script avoids sending the body at all.

  * Profile before guessing. A ProfilingSession attributes time to creation,
    enqueue, send, response and completion, which is exactly the breakdown that
    tells you whether the problem is the server, the network, or your own
    thread pool.


COMMON PITFALLS TO AVOID
========================
  * CREATING A MULTIPLEXER PER OPERATION, OR PER REQUEST. This is the big one.
    ConnectionMultiplexer is expensive to build, holds sockets, and is designed
    to be shared by the whole application. Registering it as anything other than
    a singleton exhausts connections under load and produces timeouts that look
    like a server problem. GetDatabase() is the cheap thing; call that per
    operation.

  * AWAITING A TRANSACTION'S COMMAND TASKS BEFORE CALLING Execute. They do not
    complete until the transaction executes, so awaiting first deadlocks.
    Capture the tasks, call Execute or ExecuteAsync, then await. The shipped
    analyzer reports this.

  * GUARDING A COMMAND WITH A CONDITION IT ALREADY EXPRESSES. A transaction whose
    only condition restates an argument the command itself takes -
    Condition.KeyNotExists("k") guarding StringSetAsync("k", v) - is a WATCH and
    a round-trip bought for nothing: StringSet(key, value, When.NotExists) is one
    atomic command. The shipped analyzer reports the shape as SER300, and a
    newer-server variant of it as SER301.

  * TREATING A MISSING VALUE AS null. A missing RedisValue is RedisValue.Null,
    not a null reference. Test with .IsNull or .IsNullOrEmpty. Casting a missing
    value to long throws; cast to long? instead.

  * IGNORING THE RESULT OF A LOCK ACQUISITION. CreateLockAsync returning does not
    mean the lock was taken. Check IsAcquired, every time.

  * TAKING A REDLOCK AGAINST ONE SERVER AND CALLING IT SAFE. The algorithm needs a
    quorum of independent instances. One instance - or one primary plus its
    replicas, which is the same failure domain - gives a convenience lock, not a
    correctness guarantee.

  * RUNNING KEYS AGAINST PRODUCTION. Use server.Keys(), which issues a cursored
    SCAN. A raw KEYS blocks the server for the duration of a full keyspace walk.

  * USING GetServer FOR ORDINARY DATA ACCESS. It targets one server; in a cluster
    that means you have bypassed slot routing. Use GetDatabase for data.

  * ASSUMING PUB/SUB HANDLERS ARE ORDERED. The Action-based Subscribe overload may
    run handlers concurrently. If order matters, use the ChannelMessageQueue
    overload, which is sequential by construction.

  * LEAVING AbortOnConnectFail AT ITS DEFAULT IN A SERVICE. The default throws if
    Redis is unavailable at startup, which turns a transient cache outage into a
    failed deployment. Set it to false and let the multiplexer reconnect.

  * TURNING ON AllowAdmin IN APPLICATION CONFIGURATION. It unlocks FlushDatabase
    and friends. Enable it in the tool that needs it, not in the service.

  * BLAMING THE SERVER FOR A TIMEOUT WITHOUT READING THE MESSAGE. The exception
    text carries queue depths and thread-pool counts. More often than not it
    says the client's thread pool was starved, not that Redis was slow.

  * EXPECTING A BINARY DROP-IN. This is a SOURCE-compatible replacement. Types
    and members match upstream, but the assembly and namespaces are different,
    so a consumer must recompile with the new using directives. An assembly
    compiled against StackExchange.Redis will not bind to this one.


WHAT THIS PACKAGE DOES NOT DO
=============================
  * It is not a Redis SERVER. It talks to one. (There is an in-process test
    server in this repository, but it is test support and is not published.)

  * It does not manage or install Redis. Provisioning a server, a cluster, or a
    container is somewhere else's job - CodeBrix.Docker is one option.

  * It is not an object-mapper, an ORM, or a caching abstraction. It exposes
    Redis commands. Serialization is yours, and so is the caching policy. If you
    want IDistributedCache semantics, write the thin adapter.

  * It does not multi-target. .NET 10 and later, only.

  * It is not binary-compatible with the packages it replaces; see the last
    pitfall above.

  * It does not add commands, options or behaviour of its own. Divergence from
    upstream would defeat the point. If something is missing here it is missing
    upstream too, and the fix belongs upstream first.

  * It does say its OWN name on the wire, and that is the one visible difference.
    The handshake reports lib-name=CodeBrix.Redis, and a connection you did not
    name yourself gets "<machine name>(CodeBrix.Redis-v<version>)" - so that is
    what CLIENT LIST and CLIENT INFO show. A client should not misreport its
    identity. lib-ver likewise reports THIS package's own version, read from its
    assembly file version, rather than the version of anything it replaces.
    Everything below that is byte-identical to upstream: command names, argument
    order and RESP framing.


WORKING EXAMPLES ON GITHUB
==========================
    https://github.com/ellisnet/CodeBrix.Redis

The test suites under tests/ are the most complete worked examples in the
repository, and they exercise real servers rather than mocks:

    tests/CodeBrix.Redis.Tests/           the client and the command surface
    tests/CodeBrix.Redis.Respite.Tests/   the RESP protocol layer
    tests/CodeBrix.Redis.TestHarness/     how the topologies are stood up

Because this is a faithful port, the upstream documentation applies verbatim
once the namespaces are translated:

    https://github.com/StackExchange/StackExchange.Redis
    https://github.com/samcook/RedLock.net
    https://redis.io/docs/latest/commands/


QUICK REFERENCE CARD
====================
    // connect once, share everywhere
    var mux = await ConnectionMultiplexer.ConnectAsync("localhost:6379");

    IDatabase   db   = mux.GetDatabase();        // cheap - call per operation
    ISubscriber sub  = mux.GetSubscriber();      // cheap
    IServer     srv  = mux.GetServer("localhost:6379");   // one server only

    // strings
    await db.StringSetAsync("k", "v", TimeSpan.FromMinutes(5));
    RedisValue v = await db.StringGetAsync("k");
    if (v.IsNull) { /* missing */ }

    // hashes / lists / sets / sorted sets
    await db.HashSetAsync("h", "field", "value");
    await db.ListRightPushAsync("l", "item");
    await db.SetAddAsync("s", "member");
    await db.SortedSetAddAsync("z", "member", score: 1.0);

    // expiry
    await db.KeyExpireAsync("k", TimeSpan.FromHours(1));
    TimeSpan? ttl = await db.KeyTimeToLiveAsync("k");

    // batch (pipelined, not atomic)
    IBatch batch = db.CreateBatch();
    Task<bool> t1 = batch.StringSetAsync("a", 1);
    Task<bool> t2 = batch.StringSetAsync("b", 2);
    batch.Execute();
    await Task.WhenAll(t1, t2);

    // transaction (atomic, with preconditions)
    ITransaction tran = db.CreateTransaction();
    tran.AddCondition(Condition.StringEqual("owner", "me"));
    Task set = tran.StringSetAsync("state", "claimed");
    if (await tran.ExecuteAsync()) { await set; }

    // pub/sub, in order
    ChannelMessageQueue q = await sub.SubscribeAsync(RedisChannel.Literal("news"));
    q.OnMessage(m => Handle(m.Message));

    // scripting, with named parameters
    LuaScript script = LuaScript.Prepare("return redis.call('get', @key)");
    RedisResult r = await script.EvaluateAsync(db, new { key = (RedisKey)"k" });

    // key prefixing
    IDatabase tenant = db.WithKeyPrefix("tenant:42:");

    // distributed lock
    await using IRedLock l = await factory.CreateLockAsync("res", TimeSpan.FromSeconds(30));
    if (l.IsAcquired) { /* exclusive */ }

    // scan, never KEYS
    foreach (RedisKey key in srv.Keys(pattern: "session:*", pageSize: 1000)) { }

    // useful flags
    CommandFlags.FireAndForget   CommandFlags.PreferReplica   CommandFlags.DemandMaster

================================================================================
LICENSE     MIT. Derived from StackExchange.Redis (Copyright (c) 2014 Stack
            Exchange), RESPite (Copyright (c) 2025 Marc Gravell) and RedLock.net
            (Copyright (c) 2018 Sam Cook), each MIT licensed. Full attribution
            and the record of every modification are in THIRD-PARTY-NOTICES.txt.
================================================================================
