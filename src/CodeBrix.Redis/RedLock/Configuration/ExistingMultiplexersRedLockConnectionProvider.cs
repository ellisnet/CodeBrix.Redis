using System;
using System.Collections.Generic;
using System.Linq;
using CodeBrix.Redis.RedLock.Internal;

namespace CodeBrix.Redis.RedLock.Configuration; //was previously: RedLockNet.SERedis.Configuration;

/// <summary>
/// A connection provider that uses existing Redis ConnectionMultiplexers
/// </summary>
public class ExistingMultiplexersRedLockConnectionProvider : RedLockConnectionProvider
{
    /// <summary>
    /// The already-connected multiplexers to run the algorithm across. One entry per independent instance.
    /// </summary>
    public IList<RedLockMultiplexer> Multiplexers { get; set; }

    /// <summary>
    /// Creates a provider with an empty multiplexer list; fill in <see cref="Multiplexers"/> before use.
    /// </summary>
    public ExistingMultiplexersRedLockConnectionProvider()
    {
        this.Multiplexers = new List<RedLockMultiplexer>();
    }

    internal override ICollection<RedisConnection> CreateRedisConnections()
    {
        if (this.Multiplexers == null || !this.Multiplexers.Any())
        {
            throw new ArgumentException("No multiplexers specified");
        }

        var connections = new List<RedisConnection>(this.Multiplexers.Count);

        foreach (var multiplexer in this.Multiplexers)
        {
            // TODO check that the ConnectionMultiplexer has reasonable settings (e.g. AbortOnConnectFail == false)

            var redisConnection = new RedisConnection
            {
                ConnectionMultiplexer = multiplexer.ConnectionMultiplexer,
                RedisDatabase = multiplexer.RedisDatabase ?? DefaultRedisDatabase,
                RedisKeyFormat = string.IsNullOrEmpty(multiplexer.RedisKeyFormat)
                    ? DefaultRedisKeyFormat
                    : multiplexer.RedisKeyFormat
            };

            connections.Add(redisConnection);
        }

        return connections;
    }

    internal override void DisposeConnections()
    {
        // do nothing, as these connections are externally managed
    }
}
