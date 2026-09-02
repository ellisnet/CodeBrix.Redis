using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CodeBrix.Redis.Respite.Messages; //was previously: RESPite.Messages;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public ref partial struct RespReader
{
    internal bool DebugEquals(in RespReader other)
        => _prefix == other._prefix
        && _length == other._length
        && _flags == other._flags
        && _bufferIndex == other._bufferIndex
        && _positionBase == other._positionBase
        && _remainingTailLength == other._remainingTailLength;

    internal new string ToString() => $"{Prefix} ({_flags}); length {_length}, {TotalAvailable} remaining";

    internal void DebugReset()
    {
        _bufferIndex = 0;
        _length = 0;
        _flags = 0;
        _prefix = RespPrefix.None;
    }

    // Note: the DEBUG-only VectorizeDisabled property is declared in RespReader.cs alongside the
    // other instance fields - an auto-property has a backing field, and a partial struct with
    // instance fields in more than one part has no defined field ordering (CS0282). See the note
    // in RespReader.Span.cs.

    private partial ReadOnlySpan<byte> ActiveBuffer { get; }

    internal readonly string BufferUtf8()
    {
        var clone = Clone();
        var active = clone.ActiveBuffer;
        var totalLen = checked((int)(active.Length + clone._remainingTailLength));
        var oversized = ArrayPool<byte>.Shared.Rent(totalLen);
        Span<byte> target = oversized.AsSpan(0, totalLen);

        while (!target.IsEmpty)
        {
            active.CopyTo(target);
            target = target.Slice(active.Length);
            if (!clone.TryMoveToNextSegment()) break;
            active = clone.ActiveBuffer;
        }
        if (!target.IsEmpty) throw new EndOfStreamException();

        var s = Encoding.UTF8.GetString(oversized, 0, totalLen);
        ArrayPool<byte>.Shared.Return(oversized);
        return s;
    }
}
