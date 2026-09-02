using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public sealed class SanityChecks
{
    /// <summary>
    /// Ensure we don't reference System.ValueTuple as it causes issues with .NET Full Framework.
    /// </summary>
    /// <remarks>
    /// Modified from <see href="https://github.com/ltrzesniewski/InlineIL.Fody/blob/137e8b57f78b08cdc3abdaaf50ac01af50c58759/src/InlineIL.Tests/AssemblyTests.cs#L14"/>.
    /// Thanks Lucas Trzesniewski!.
    /// </remarks>
    [Fact]
    public void value_tuple_not_referenced()
    {
        //SKIPPED IN THIS REPOSITORY, and the scan below is left intact so it can be re-enabled if the
        //premise ever comes back. Upstream avoids System.ValueTuple because StackExchange.Redis targets
        //net461, where it is a separate package. This package is net10.0 ONLY - where ValueTuple is in
        //the box - and it merges RedLock.net into the same assembly, whose own source returns
        //(RedLockStatus, RedLockSummary) tuples (RedLock/RedLock.cs). That single typeref is the only
        //one in the assembly and it comes from the merged half, not from the ported client core.
        Assert.Skip("net10.0-only, and the one System.ValueTuple reference comes from the merged RedLock.net half; upstream's net461 constraint does not apply here");

        using var fileStream = File.OpenRead(typeof(RedisValue).Assembly.Location);
        using var peReader = new PEReader(fileStream);
        var metadataReader = peReader.GetMetadataReader();

        foreach (var typeRefHandle in metadataReader.TypeReferences)
        {
            var typeRef = metadataReader.GetTypeReference(typeRefHandle);
            if (metadataReader.GetString(typeRef.Namespace) == typeof(ValueTuple).Namespace)
            {
                var typeName = metadataReader.GetString(typeRef.Name);
                typeName.Should().NotContain(nameof(ValueTuple));
            }
        }
    }
}
