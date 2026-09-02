using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;

namespace CodeBrix.Redis; //was previously: StackExchange.Redis;

internal static class ExtensionMethodsInternal
{
    internal static bool IsNullOrEmpty([NotNullWhen(false)] this string? s) =>
        string.IsNullOrEmpty(s);

    internal static bool IsNullOrWhiteSpace([NotNullWhen(false)] this string? s) =>
        string.IsNullOrWhiteSpace(s);

    internal static RedisKey[] AssertAllNonNull(this RedisKey[] keys)
    {
        if (keys is null) throw new ArgumentNullException(nameof(keys));
        for (var i = 0; i < keys.Length; i++)
        {
            keys[i].AssertNotNull();
        }
        return keys;
    }

    internal static RedisValue[] AssertAllNonNull(this RedisValue[] values)
    {
        if (values is null) throw new ArgumentNullException(nameof(values));
        for (var i = 0; i < values.Length; i++)
        {
            values[i].AssertNotNull();
        }
        return values;
    }

}
