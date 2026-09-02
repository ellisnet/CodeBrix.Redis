namespace CodeBrix.Redis.RedLock.Internal; //was previously: RedLockNet.SERedis.Internal;

internal enum RedLockInstanceResult
{
    Success,
    Conflicted,
    Error
}
