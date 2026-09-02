namespace CodeBrix.Redis; //was previously: StackExchange.Redis;

internal enum WriteResult
{
    Success,
    NoConnectionAvailable,
    TimeoutBeforeWrite,
    WriteFailure,
}
