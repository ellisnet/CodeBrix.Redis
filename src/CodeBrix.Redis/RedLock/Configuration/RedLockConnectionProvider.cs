using System.Collections.Generic;
using CodeBrix.Redis.RedLock.Internal;

namespace CodeBrix.Redis.RedLock.Configuration; //was previously: RedLockNet.SERedis.Configuration;

/// <summary>
/// Supplies the set of Redis connections that the Redlock algorithm runs across, and owns whatever
/// part of their lifetime it created.
/// </summary>
public abstract class RedLockConnectionProvider
{
    internal abstract ICollection<RedisConnection> CreateRedisConnections();
    internal abstract void DisposeConnections();

    /// <summary>
    /// The database used when an endpoint does not name one: -1, meaning the connection's own default.
    /// </summary>
    protected const int DefaultRedisDatabase = -1;

    /// <summary>
    /// The key format used when an endpoint does not name one: "redlock:{0}", where {0} is the resource name.
    /// </summary>
    protected const string DefaultRedisKeyFormat = "redlock:{0}";
}
