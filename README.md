# CodeBrix.Redis

A high-performance, fully managed **Redis client** for .NET, incorporating both synchronous and
asynchronous usage. One assembly covers the whole stack: the RESP protocol reader and writer, the
connection multiplexer and its cluster / sentinel / replica awareness, the full command surface,
and the Redlock distributed-lock algorithm.

Published on NuGet as **`CodeBrix.Redis.MitLicenseForever`** — MIT licensed, forever.

CodeBrix.Redis is a port of three MIT-licensed libraries into a single .NET 10 package:

| Upstream package | Version ported | Becomes |
|---|---|---|
| [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis) | 3.1.31 | `CodeBrix.Redis` |
| [RESPite](https://github.com/StackExchange/StackExchange.Redis) | 3.1.31 | `CodeBrix.Redis.Respite` |
| [RedLock.net](https://github.com/samcook/RedLock.net) | 2.3.2 | `CodeBrix.Redis.RedLock` |

It is a drop-in replacement for all three: the types, members, and behaviour are those of the
upstream libraries. The one change a consumer must make is to the `using` directives — the
namespaces are the `CodeBrix.Redis.*` names above. Full attribution and the record of every
modification made during the port are in `THIRD-PARTY-NOTICES.txt`.

CodeBrix.Redis takes exactly two NuGet dependencies, both published by Microsoft:
`Microsoft.Extensions.Logging.Abstractions` (its `ILoggerFactory` is part of the public surface)
and `System.IO.Hashing`.

CodeBrix.Redis supports applications and assemblies that target Microsoft .NET version 10.0 and
later. Microsoft .NET version 10.0 is a Long-Term Supported (LTS) version of .NET, and was
released on Nov 11, 2025; and will be actively supported by Microsoft until Nov 14, 2028.
Please update your C#/.NET code and projects to the latest LTS version of Microsoft .NET.

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
* **A low-level RESP layer** (`CodeBrix.Redis.Respite`) usable on its own, for talking RESP to
  something that is not Redis
* **The Redlock distributed-lock algorithm** (`CodeBrix.Redis.RedLock`) across independent Redis
  instances, with lock extension and expiry
* **Roslyn analyzers shipped in the package** — the same compile-time diagnostics the upstream
  package provides, including the transaction and queued-result analyzers, with code fixes

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

## License

The project is licensed under the MIT License. see: https://en.wikipedia.org/wiki/MIT_License

CodeBrix.Redis is derived from StackExchange.Redis (Copyright (c) 2014 Stack Exchange), RESPite
(Copyright (c) 2025 Marc Gravell), and RedLock.net (Copyright (c) 2018 Sam Cook), each MIT
licensed. See `THIRD-PARTY-NOTICES.txt`.
