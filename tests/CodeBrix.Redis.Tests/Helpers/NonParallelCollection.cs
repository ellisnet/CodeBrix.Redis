using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public static class NonParallelCollection
{
    public const string Name = "NonParallel";
}
