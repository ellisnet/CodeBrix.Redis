using System;
using System.IO;
using System.Reflection;

namespace CodeBrix.Redis.RedLock.Util; //was previously: RedLockNet.SERedis.Util;

internal static class EmbeddedResourceLoader
{
    internal static string GetEmbeddedResource(string name)
    {
        var assembly = typeof(EmbeddedResourceLoader).GetTypeInfo().Assembly;

        //the resource names are pinned with LogicalName in the csproj, so a rename of the assembly
        //or of the folder cannot silently change them; a miss is a build/packaging defect, not a
        //runtime condition to recover from
        using (var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"The embedded resource '{name}' was not found in assembly '{assembly.FullName}'."))
        using (var streamReader = new StreamReader(stream))
        {
            return streamReader.ReadToEnd();
        }
    }
}
