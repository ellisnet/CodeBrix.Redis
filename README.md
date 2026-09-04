# CodeBrix.Redis

A high-performance, fully managed **Redis client** for .NET, incorporating both synchronous and
asynchronous usage. One assembly covers the whole stack: the RESP protocol reader and writer, the
connection multiplexer and its cluster / sentinel / replica awareness, the full command surface,
and the Redlock distributed-lock algorithm. CodeBrix.Redis is provided as a .NET 10 library and
associated `CodeBrix.Redis.MitLicenseForever` NuGet package.

CodeBrix.Redis supports applications and assemblies that target Microsoft .NET version 10.0 and later.
Microsoft .NET version 10.0 is a Long-Term Supported (LTS) version of .NET, and was released on Nov 11, 2025; and will be actively supported by Microsoft until Nov 14, 2028.
Please update your C#/.NET code and projects to the latest LTS version of Microsoft .NET.

## Installation

```
dotnet add package CodeBrix.Redis.MitLicenseForever
```

Note that the NuGet package ID and the namespace are different - there is no package named plain `CodeBrix.Redis`:

* NuGet package ID: `CodeBrix.Redis.MitLicenseForever`
* Assembly and primary namespace: `CodeBrix.Redis` - i.e. `using CodeBrix.Redis;`

XML documentation (IntelliSense) ships alongside the assembly.

The package pulls in exactly two dependencies, both published by Microsoft; no version pinning is
needed in the consuming project:

* `Microsoft.Extensions.Logging.Abstractions` - its `ILoggerFactory` is part of the public surface,
  through `ConfigurationOptions.LoggerFactory`
* `System.IO.Hashing` - the hashing behind cluster hash-slot routing

Roslyn analyzers, and their code fixes for the IDE, install themselves along with the package. They
are compile-time only, and nothing about them reaches your output.

## CodeBrix.Redis supports:

* **Connection multiplexing** — one `ConnectionMultiplexer` shared across an entire application,
  with automatic reconnection, a configurable backlog policy, and connection-event notifications
* **Every server topology** — standalone, primary/replica, Redis Cluster (with hash-slot routing
  and `MOVED`/`ASK` following), and Sentinel-managed failover
* **The full command surface** — strings, hashes, lists, sets, sorted sets, bitmaps, HyperLogLog,
  geospatial indexes, streams, and vector sets, each in synchronous and asynchronous form
* **RESP2 and RESP3**, negotiated with `HELLO`, including RESP3 push messages
* **Publish/subscribe**, including `ChannelMessageQueue` for ordered, sequential consumption and
  keyspace notifications
* **Transactions and batches** — `ITransaction` with `Condition` preconditions, and `IBatch` for
  pipelined non-atomic sends
* **Lua scripting** — `LuaScript` with named-parameter mapping, script caching, and `EVALSHA`
* **Key-space isolation** — a prefixed `IDatabase` view, so several logical applications can share
  one Redis database without colliding
* **Profiling and diagnostics** — per-command profiling sessions, connection counters, storm logs,
  and an `ILoggerFactory` hook for connection logging
* **A low-level RESP layer** (namespace `CodeBrix.Redis.Respite`) usable on its own, for talking
  RESP to something that is not Redis
* **The Redlock distributed-lock algorithm** (namespace `CodeBrix.Redis.RedLock`) across
  independent Redis instances, with lock extension and expiry
* **Roslyn analyzers shipped in the package** — compile-time transaction and queued-result
  diagnostics, with code fixes in the IDE

## Sample Code

### Connect, and read and write a value

```csharp
using CodeBrix.Redis;

using var multiplexer = await ConnectionMultiplexer.ConnectAsync("localhost:6379");

IDatabase db = multiplexer.GetDatabase();

await db.StringSetAsync("greeting", "hello world");
RedisValue value = await db.StringGetAsync("greeting");

Console.WriteLine(value);   // hello world
```

### Take a distributed lock

```csharp
using CodeBrix.Redis;
using CodeBrix.Redis.RedLock;
using CodeBrix.Redis.RedLock.Configuration;

using var multiplexer = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
using var factory = RedLockFactory.Create([new RedLockMultiplexer(multiplexer)]);

await using var redLock = await factory.CreateLockAsync(
    resource: "order-42",
    expiryTime: TimeSpan.FromSeconds(30));

if (redLock.IsAcquired)
{
    // exclusive work here
}
```

## Documentation

The NuGet package includes `AGENT-README.txt`, a complete API reference and usage guide written for AI coding agents - point your agent at that file when it is writing code against this library.

Additional sample code and usage examples are available in the `CodeBrix.Redis.Tests` project:
https://github.com/ellisnet/CodeBrix.Redis/tree/main/tests/CodeBrix.Redis.Tests

## License

CodeBrix.Redis is licensed under the MIT License - see the
[LICENSE](https://github.com/ellisnet/CodeBrix.Redis/blob/main/LICENSE) file.

For licensing and provenance information about the open source code included in
this package, see [THIRD-PARTY-NOTICES.txt](https://github.com/ellisnet/CodeBrix.Redis/blob/main/THIRD-PARTY-NOTICES.txt).
