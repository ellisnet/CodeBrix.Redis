using System;
using System.Runtime.InteropServices;

namespace CodeBrix.Redis; //was previously: StackExchange.Redis;

internal static class Runtime
{
    public static readonly bool IsMono = RuntimeInformation.FrameworkDescription.StartsWith("Mono ", StringComparison.OrdinalIgnoreCase);
}
