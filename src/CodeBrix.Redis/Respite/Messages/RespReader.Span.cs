using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CodeBrix.Redis.Respite.Messages; //was previously: RESPite.Messages;

/*
 How we actually implement the underlying buffer depends on the capabilities of the runtime.
 */

public ref partial struct RespReader
{
    // Note: _bufferRoot and _bufferLength are declared in RespReader.cs alongside the other
    // instance fields, rather than here as upstream has them. Upstream carried a
    // "#pragma warning disable CS0282" in every partial part of this type because a partial
    // struct with instance fields in more than one part has no defined field ordering; this
    // repository suppresses no warnings, so the fields are declared once instead. Nothing else
    // about them changed - they are still used only by this part.

    private partial void UnsafeTrimCurrentBy(int count)
    {
        Debug.Assert(count >= 0 && count <= _bufferLength, "Unsafe trim length");
        _bufferLength -= count;
    }

    private readonly partial ref byte UnsafeCurrent
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.Add(ref _bufferRoot, _bufferIndex);
    }

    private readonly partial int CurrentLength
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _bufferLength;
    }

    private readonly partial ReadOnlySpan<byte> CurrentSpan() => MemoryMarshal.CreateReadOnlySpan(
        ref UnsafeCurrent, CurrentAvailable);

    private readonly partial ReadOnlySpan<byte> UnsafePastPrefix() => MemoryMarshal.CreateReadOnlySpan(
        ref Unsafe.Add(ref _bufferRoot, _bufferIndex + 1),
        _bufferLength - (_bufferIndex + 1));

    private partial void SetCurrent(ReadOnlySpan<byte> value)
    {
        _bufferRoot = ref MemoryMarshal.GetReference(value);
        _bufferLength = value.Length;
    }
    private partial ReadOnlySpan<byte> ActiveBuffer => MemoryMarshal.CreateReadOnlySpan(ref _bufferRoot, _bufferLength);
}
