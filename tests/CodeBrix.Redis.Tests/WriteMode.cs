using CodeBrix.Redis.Respite.Streams;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public enum WriteMode
{
    Default = (int)BufferedStreamWriter.WriteMode.Default,
    Sync = (int)BufferedStreamWriter.WriteMode.Sync,
    Async = (int)BufferedStreamWriter.WriteMode.Async,
    Pipe = (int)BufferedStreamWriter.WriteMode.Pipe,
}
