using CodeBrix.Redis;

namespace CodeBrix.Redis.RedLock.Configuration; //was previously: RedLockNet.SERedis.Configuration;

/// <summary>
/// An already-connected multiplexer offered to the Redlock algorithm as one independent instance,
/// together with the database and key format to use on it.
/// </summary>
public class RedLockMultiplexer
{
    /// <summary>
    /// The connection this instance is reached through. Its lifetime stays with whoever created it.
    /// </summary>
    public IConnectionMultiplexer ConnectionMultiplexer { get; }

    /// <summary>
    /// Wraps an existing connection so it can be used as one Redlock instance.
    /// </summary>
    /// <param name="connectionMultiplexer">The connection to use.</param>
    public RedLockMultiplexer(IConnectionMultiplexer connectionMultiplexer)
    {
        this.ConnectionMultiplexer = connectionMultiplexer;
    }

    /// <summary>
    /// Wraps an existing connection so it can be used as one Redlock instance.
    /// </summary>
    /// <param name="connectionMultiplexer">The connection to use.</param>
    public static implicit operator RedLockMultiplexer(ConnectionMultiplexer connectionMultiplexer)
    {
        return new RedLockMultiplexer(connectionMultiplexer);
    }

    /// <summary>
    /// The database to use with this redis connection.
    /// Defaults to the ConnectionMultiplexer's default database if not specified.
    /// </summary>
    public int? RedisDatabase { get; set; }

    /// <summary>
    /// The string format for keys created in redis, must include {0}.
    /// Defaults to "redlock:{0}" if not specified.
    /// </summary>
    public string? RedisKeyFormat { get; set; }
}
