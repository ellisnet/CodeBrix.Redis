using CodeBrix.Redis;

namespace CodeBrix.Redis.RedLock.Internal; //was previously: RedLockNet.SERedis.Internal;

internal class RedisConnection
{
    //required: every construction site sets these in an object initializer, and nullable reference
    //types are on here, unlike upstream
    public required IConnectionMultiplexer ConnectionMultiplexer { get; set; }
    public int RedisDatabase { get; set; }
    public required string RedisKeyFormat { get; set; }
}
