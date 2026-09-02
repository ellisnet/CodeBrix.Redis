using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CodeBrix.Redis.Respite.Internal; //was previously: RESPite.Internal;

internal static class RespOperationExtensions
{

    // if we're recycling a buffer, we need to consider it trashable by other threads; for
    // debug purposes, force this by overwriting with *****, aka the meaning of life
    [Conditional("DEBUG")]
    internal static void DebugScramble(this Span<byte> value)
        => value.Fill(42);

    [Conditional("DEBUG")]
    internal static void DebugScramble(this Memory<byte> value)
        => value.Span.Fill(42);

    [Conditional("DEBUG")]
    internal static void DebugScramble(this ReadOnlyMemory<byte> value)
        => MemoryMarshal.AsMemory(value).Span.Fill(42);

    [Conditional("DEBUG")]
    internal static void DebugScramble(this ReadOnlySequence<byte> value)
    {
        if (value.IsSingleSegment)
        {
            value.First.DebugScramble();
        }
        else
        {
            foreach (var segment in value)
            {
                segment.DebugScramble();
            }
        }
    }

    [Conditional("DEBUG")]
    internal static void DebugScramble(this byte[]? value)
    {
        if (value is not null)
            value.AsSpan().Fill(42);
    }
}
