using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// This file is compiled into BOTH src/CodeBrix.Redis (net10.0) and src/CodeBrix.Redis.Build
// (netstandard2.0, see the <Compile Include> items in CodeBrix.Redis.Build.csproj): the
// generator and the runtime have to agree byte-for-byte on the hash, so they compile the same
// source. That is why the #if NET directive in ToUC survives the port - MemoryMarshal.CreateSpan
// is in the box for net10.0 and absent on netstandard2.0, so both branches are still reachable.
namespace CodeBrix.Redis.Respite; //was previously: RESPite;

/// <summary>
/// This type is intended to provide fast hashing functions for small ASCII strings, for example well-known
/// RESP literals that are usually identifiable by their length and initial bytes; it is not intended
/// for general purpose hashing, and the behavior is undefined for non-ASCII literals.
/// All matches must also perform a sequence equality check.
/// </summary>
/// <param name="token">The token expected when parsing data, if different from the implied value.</param>
/// <remarks>See HastHashGenerator.md for more information and intended usage.</remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Enum,
    AllowMultiple = false,
    Inherited = false)]
[Conditional("DEBUG")] // evaporate in release
[Experimental(Experiments.Respite, UrlFormat = Experiments.UrlFormat)]
public sealed partial class AsciiHashAttribute(string token = "") : Attribute
{
    /// <summary>
    /// The token expected when parsing data, if different from the implied value. The implied
    /// value is the name, replacing underscores for hyphens, so: 'a_b' becomes 'a-b'.
    /// </summary>
    /// <remarks>An explicit empty token (i.e. <c>[AsciiHash("")]</c>) means that the member is
    /// excluded, and cannot be parsed or formatted; this is for client-side values such as
    /// <c>Unknown</c>. This is distinct from omitting the token, which infers it from the name.</remarks>
    public string Token => token;

    /// <summary>
    /// Indicates whether a parse operation is case-sensitive. Not used in other contexts.
    /// </summary>
    public bool CaseSensitive { get; set; } = true;
}

// note: instance members are in AsciiHash.Instance.cs.

/// <summary>
/// A short ASCII token together with the case-sensitive and case-insensitive hashes of its
/// leading bytes, for cheap comparison of RESP literals such as command and reply names.
/// </summary>
/// <remarks>
/// The hashes pack up to <c>sizeof(long)</c> bytes of the value directly into a <see cref="long"/>,
/// so for values of that length or shorter a hash comparison IS an equality comparison; longer
/// values still require a sequence equality check. Behavior is undefined for non-ASCII data.
/// </remarks>
[Experimental(Experiments.Respite, UrlFormat = Experiments.UrlFormat)]
public readonly partial struct AsciiHash
{
    /// <summary>
    /// In-place ASCII upper-case conversion.
    /// </summary>
    /// <param name="span">The buffer to convert in place.</param>
    public static void ToUpper(Span<byte> span)
    {
        foreach (ref var b in span)
        {
            if (b >= 'a' && b <= 'z')
                b = (byte)(b & ~0x20);
        }
    }

    /// <summary>
    /// In-place ASCII lower-case conversion.
    /// </summary>
    /// <param name="span">The buffer to convert in place.</param>
    public static void ToLower(Span<byte> span)
    {
        foreach (ref var b in span)
        {
            if (b >= 'a' && b <= 'z')
                b |= (byte)(b & ~0x20);
        }
    }

    internal const int MaxBytesHashed = sizeof(long);

    /// <summary>
    /// Tests two ASCII values for case-sensitive equality, using the hash as a shortcut when both
    /// are short enough for it to be exact.
    /// </summary>
    /// <param name="first">The first value to compare.</param>
    /// <param name="second">The second value to compare.</param>
    /// <returns><c>true</c> if the two values are the same length and hold the same bytes.</returns>
    public static bool EqualsCS(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        var len = first.Length;
        if (len != second.Length) return false;
        // for very short values, the CS hash performs CS equality
        return len <= MaxBytesHashed ? HashCS(first) == HashCS(second) : first.SequenceEqual(second);
    }

    /// <summary>
    /// Tests two ASCII values for case-sensitive equality, comparing every byte rather than
    /// taking the hash shortcut.
    /// </summary>
    /// <param name="first">The first value to compare.</param>
    /// <param name="second">The second value to compare.</param>
    /// <returns><c>true</c> if the two values are the same length and hold the same bytes.</returns>
    public static bool SequenceEqualsCS(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
        => first.SequenceEqual(second);

    /// <summary>
    /// Tests two ASCII values for case-insensitive equality, using the hash as a shortcut when
    /// both are short enough for it to be exact.
    /// </summary>
    /// <param name="first">The first value to compare.</param>
    /// <param name="second">The second value to compare.</param>
    /// <returns><c>true</c> if the two values match, ignoring ASCII case.</returns>
    public static bool EqualsCI(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        var len = first.Length;
        if (len != second.Length) return false;
        // for very short values, the UC hash performs CI equality
        return len <= MaxBytesHashed ? HashUC(first) == HashUC(second) : SequenceEqualsCI(first, second);
    }

    /// <summary>
    /// Tests an ASCII byte value and a UTF-16 value for case-insensitive equality.
    /// </summary>
    /// <param name="first">The ASCII value to compare.</param>
    /// <param name="second">The character value to compare.</param>
    /// <returns><c>true</c> if the two values match, ignoring ASCII case.</returns>
    public static bool EqualsCI(ReadOnlySpan<byte> first, ReadOnlySpan<char> second)
        => EqualsCI(second, first);

    /// <summary>
    /// Tests two ASCII values for case-insensitive equality, comparing every byte rather than
    /// taking the hash shortcut.
    /// </summary>
    /// <param name="first">The first value to compare.</param>
    /// <param name="second">The second value to compare.</param>
    /// <returns><c>true</c> if the two values match, ignoring ASCII case.</returns>
    public static unsafe bool SequenceEqualsCI(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        var len = first.Length;
        if (len != second.Length) return false;

        // OK, don't be clever (SIMD, etc); the purpose of FashHash is to compare RESP key tokens, which are
        // typically relatively short, think 3-20 bytes. That wouldn't even touch a SIMD vector, so:
        // just loop (the exact thing we'd need to do *anyway* in a SIMD implementation, to mop up the non-SIMD
        // trailing bytes).
        fixed (byte* firstPtr = &MemoryMarshal.GetReference(first))
        {
            fixed (byte* secondPtr = &MemoryMarshal.GetReference(second))
            {
                const int CS_MASK = 0b0101_1111;
                for (int i = 0; i < len; i++)
                {
                    byte x = firstPtr[i];
                    var xCI = x & CS_MASK;
                    if (xCI >= 'A' & xCI <= 'Z')
                    {
                        // alpha mismatch
                        if (xCI != (secondPtr[i] & CS_MASK)) return false;
                    }
                    else if (x != secondPtr[i])
                    {
                        // non-alpha mismatch
                        return false;
                    }
                }

                return true;
            }
        }
    }

    /// <summary>
    /// Tests an ASCII byte value and a UTF-16 value for case-insensitive equality, comparing
    /// every element rather than taking the hash shortcut.
    /// </summary>
    /// <param name="first">The ASCII value to compare.</param>
    /// <param name="second">The character value to compare.</param>
    /// <returns><c>true</c> if the two values match, ignoring ASCII case.</returns>
    public static bool SequenceEqualsCI(ReadOnlySpan<byte> first, ReadOnlySpan<char> second)
        => SequenceEqualsCI(second, first);

    /// <summary>
    /// Tests two UTF-16 values for case-sensitive equality, using the hash as a shortcut when
    /// both are short enough for it to be exact.
    /// </summary>
    /// <param name="first">The first value to compare.</param>
    /// <param name="second">The second value to compare.</param>
    /// <returns><c>true</c> if the two values are the same length and hold the same characters.</returns>
    public static bool EqualsCS(ReadOnlySpan<char> first, ReadOnlySpan<char> second)
    {
        var len = first.Length;
        if (len != second.Length) return false;
        // for very short values, the CS hash performs CS equality
        return len <= MaxBytesHashed ? HashCS(first) == HashCS(second) : first.SequenceEqual(second);
    }

    /// <summary>
    /// Tests two UTF-16 values for case-sensitive equality, comparing every character rather
    /// than taking the hash shortcut.
    /// </summary>
    /// <param name="first">The first value to compare.</param>
    /// <param name="second">The second value to compare.</param>
    /// <returns><c>true</c> if the two values are the same length and hold the same characters.</returns>
    public static bool SequenceEqualsCS(ReadOnlySpan<char> first, ReadOnlySpan<char> second)
        => first.SequenceEqual(second);

    /// <summary>
    /// Tests two UTF-16 values for case-insensitive equality, using the hash as a shortcut when
    /// both are short enough for it to be exact.
    /// </summary>
    /// <param name="first">The first value to compare.</param>
    /// <param name="second">The second value to compare.</param>
    /// <returns><c>true</c> if the two values match, ignoring ASCII case.</returns>
    public static bool EqualsCI(ReadOnlySpan<char> first, ReadOnlySpan<char> second)
    {
        var len = first.Length;
        if (len != second.Length) return false;
        // for very short values, the CS hash performs CS equality; check that first
        return len <= MaxBytesHashed ? HashUC(first) == HashUC(second) : SequenceEqualsCI(first, second);
    }

    /// <summary>
    /// Tests a UTF-16 value and an ASCII byte value for case-insensitive equality, using the hash
    /// as a shortcut when both are short enough for it to be exact.
    /// </summary>
    /// <param name="first">The character value to compare.</param>
    /// <param name="second">The ASCII value to compare.</param>
    /// <returns><c>true</c> if the two values match, ignoring ASCII case.</returns>
    public static bool EqualsCI(ReadOnlySpan<char> first, ReadOnlySpan<byte> second)
    {
        var len = first.Length;
        if (len != second.Length) return false;
        // for very short values, the UC hash performs CI equality
        return len <= MaxBytesHashed ? HashUC(first) == HashUC(second) : SequenceEqualsCI(first, second);
    }

    /// <summary>
    /// Tests two UTF-16 values for case-insensitive equality, comparing every character rather
    /// than taking the hash shortcut.
    /// </summary>
    /// <param name="first">The first value to compare.</param>
    /// <param name="second">The second value to compare.</param>
    /// <returns><c>true</c> if the two values match, ignoring ASCII case.</returns>
    public static unsafe bool SequenceEqualsCI(ReadOnlySpan<char> first, ReadOnlySpan<char> second)
    {
        var len = first.Length;
        if (len != second.Length) return false;

        // OK, don't be clever (SIMD, etc); the purpose of FashHash is to compare RESP key tokens, which are
        // typically relatively short, think 3-20 bytes. That wouldn't even touch a SIMD vector, so:
        // just loop (the exact thing we'd need to do *anyway* in a SIMD implementation, to mop up the non-SIMD
        // trailing bytes).
        fixed (char* firstPtr = &MemoryMarshal.GetReference(first))
        {
            fixed (char* secondPtr = &MemoryMarshal.GetReference(second))
            {
                const int CS_MASK = 0b0101_1111;
                for (int i = 0; i < len; i++)
                {
                    int x = (byte)firstPtr[i];
                    var xCI = x & CS_MASK;
                    if (xCI >= 'A' & xCI <= 'Z')
                    {
                        // alpha mismatch
                        if (xCI != (secondPtr[i] & CS_MASK)) return false;
                    }
                    else if (x != (byte)secondPtr[i])
                    {
                        // non-alpha mismatch
                        return false;
                    }
                }

                return true;
            }
        }
    }

    /// <summary>
    /// Tests a UTF-16 value and an ASCII byte value for case-insensitive equality, comparing
    /// every element rather than taking the hash shortcut.
    /// </summary>
    /// <param name="first">The character value to compare.</param>
    /// <param name="second">The ASCII value to compare.</param>
    /// <returns><c>true</c> if the two values match, ignoring ASCII case.</returns>
    public static unsafe bool SequenceEqualsCI(ReadOnlySpan<char> first, ReadOnlySpan<byte> second)
    {
        var len = first.Length;
        if (len != second.Length) return false;

        // OK, don't be clever (SIMD, etc); the purpose of FashHash is to compare RESP key tokens, which are
        // typically relatively short, think 3-20 bytes. That wouldn't even touch a SIMD vector, so:
        // just loop (the exact thing we'd need to do *anyway* in a SIMD implementation, to mop up the non-SIMD
        // trailing bytes).
        fixed (char* firstPtr = &MemoryMarshal.GetReference(first))
        {
            fixed (byte* secondPtr = &MemoryMarshal.GetReference(second))
            {
                const int CS_MASK = 0b0101_1111;
                for (int i = 0; i < len; i++)
                {
                    int x = (byte)firstPtr[i];
                    var xCI = x & CS_MASK;
                    if (xCI >= 'A' & xCI <= 'Z')
                    {
                        // alpha mismatch
                        if (xCI != (secondPtr[i] & CS_MASK)) return false;
                    }
                    else if (x != secondPtr[i])
                    {
                        // non-alpha mismatch
                        return false;
                    }
                }

                return true;
            }
        }
    }

    /// <summary>
    /// Computes both the case-sensitive and the upper-cased hash of the leading bytes of a value.
    /// </summary>
    /// <param name="value">The value to hash.</param>
    /// <param name="cs">The case-sensitive hash.</param>
    /// <param name="uc">The hash of the same bytes with ASCII lower-case folded to upper-case.</param>
    public static void Hash(scoped ReadOnlySpan<byte> value, out long cs, out long uc)
    {
        cs = HashCS(value);
        uc = ToUC(cs);
    }

    /// <summary>
    /// Computes both the case-sensitive and the upper-cased hash of the leading characters of a value.
    /// </summary>
    /// <param name="value">The value to hash.</param>
    /// <param name="cs">The case-sensitive hash.</param>
    /// <param name="uc">The hash of the same characters with ASCII lower-case folded to upper-case.</param>
    public static void Hash(scoped ReadOnlySpan<char> value, out long cs, out long uc)
    {
        cs = HashCS(value);
        uc = ToUC(cs);
    }

    /// <summary>
    /// Computes the hash of the leading bytes of a value, with ASCII lower-case folded to upper-case
    /// so that the result compares case-insensitively.
    /// </summary>
    /// <param name="value">The value to hash.</param>
    /// <returns>The case-insensitive hash.</returns>
    public static long HashUC(scoped ReadOnlySpan<byte> value) => ToUC(HashCS(value));

    /// <summary>
    /// Computes the hash of the leading characters of a value, with ASCII lower-case folded to
    /// upper-case so that the result compares case-insensitively.
    /// </summary>
    /// <param name="value">The value to hash.</param>
    /// <returns>The case-insensitive hash.</returns>
    public static long HashUC(scoped ReadOnlySpan<char> value) => ToUC(HashCS(value));

    internal static long ToUC(long hashCS)
    {
        const long LC_MASK = 0x2020_2020_2020_2020;
        // check whether there are any possible lower-case letters;
        // this would be anything with the 0x20 bit set
        if ((hashCS & LC_MASK) == 0) return hashCS;

        // Something looks possibly lower-case; we can't just mask it off,
        // because there are other non-alpha characters in that range.
#if NET
        ToUpper(MemoryMarshal.CreateSpan(ref Unsafe.As<long, byte>(ref hashCS), sizeof(long)));
        return hashCS;
#else
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, hashCS);
        ToUpper(buffer);
        return BinaryPrimitives.ReadInt64LittleEndian(buffer);
#endif
    }

    /// <summary>
    /// Computes the case-sensitive hash of the leading bytes of a value, by packing up to eight
    /// of them into a <see cref="long"/> in little-endian order.
    /// </summary>
    /// <param name="value">The value to hash.</param>
    /// <returns>The case-sensitive hash.</returns>
    public static long HashCS(scoped ReadOnlySpan<byte> value)
    {
        // at least 8? we can blit
        if ((value.Length >> 3) != 0) return BinaryPrimitives.ReadInt64LittleEndian(value);

        // small (<7); manual loop
        // note: profiling with unsafe code to pick out elements: much slower
        // note: profiling with overstamping a local: 3x slower
        ulong tally = 0;
        for (int i = 0; i < value.Length; i++)
        {
            tally |= ((ulong)value[i]) << (i << 3);
        }
        return (long)tally;
    }

    /// <summary>
    /// Computes the case-sensitive hash of the leading characters of a value, by packing the low
    /// byte of up to eight of them into a <see cref="long"/> in little-endian order.
    /// </summary>
    /// <param name="value">The value to hash.</param>
    /// <returns>The case-sensitive hash.</returns>
    public static long HashCS(scoped ReadOnlySpan<char> value)
    {
        // note: BDN profiling with Vector64.Narrow showed no benefit
        if ((value.Length >> 3) != 0)
        {
            // slice if necessary, so we can use bounds-elided foreach
            if (value.Length != 8) value = value.Slice(0, 8);
        }
        ulong tally = 0;
        for (int i = 0; i < value.Length; i++)
        {
            tally |= ((ulong)value[i]) << (i << 3);
        }
        return (long)tally;
    }

    /// <summary>
    /// Computes the case-sensitive hashes of the first and second eight-byte groups of a value.
    /// </summary>
    /// <param name="value">The value to hash.</param>
    /// <param name="cs0">The hash of the first eight bytes.</param>
    /// <param name="cs1">The hash of the next eight bytes, or zero if the value is not that long.</param>
    public static void HashCS(scoped ReadOnlySpan<byte> value, out long cs0, out long cs1)
    {
        cs0 = HashCS(value);
        cs1 = value.Length > MaxBytesHashed ? HashCS(value.Slice(start: MaxBytesHashed)) : 0;
    }

    /// <summary>
    /// Computes the case-sensitive hashes of the first and second eight-character groups of a value.
    /// </summary>
    /// <param name="value">The value to hash.</param>
    /// <param name="cs0">The hash of the first eight characters.</param>
    /// <param name="cs1">The hash of the next eight characters, or zero if the value is not that long.</param>
    public static void HashCS(scoped ReadOnlySpan<char> value, out long cs0, out long cs1)
    {
        cs0 = HashCS(value);
        cs1 = value.Length > MaxBytesHashed ? HashCS(value.Slice(start: MaxBytesHashed)) : 0;
    }

    /// <summary>
    /// Computes the case-insensitive hashes of the first and second eight-byte groups of a value.
    /// </summary>
    /// <param name="value">The value to hash.</param>
    /// <param name="cs0">The hash of the first eight bytes.</param>
    /// <param name="cs1">The hash of the next eight bytes, or zero if the value is not that long.</param>
    public static void HashUC(scoped ReadOnlySpan<byte> value, out long cs0, out long cs1)
    {
        cs0 = HashUC(value);
        cs1 = value.Length > MaxBytesHashed ? HashUC(value.Slice(start: MaxBytesHashed)) : 0;
    }

    /// <summary>
    /// Computes the case-insensitive hashes of the first and second eight-character groups of a value.
    /// </summary>
    /// <param name="value">The value to hash.</param>
    /// <param name="cs0">The hash of the first eight characters.</param>
    /// <param name="cs1">The hash of the next eight characters, or zero if the value is not that long.</param>
    public static void HashUC(scoped ReadOnlySpan<char> value, out long cs0, out long cs1)
    {
        cs0 = HashUC(value);
        cs1 = value.Length > MaxBytesHashed ? HashUC(value.Slice(start: MaxBytesHashed)) : 0;
    }

    /// <summary>
    /// Computes the case-sensitive and case-insensitive hashes of the first and second eight-byte
    /// groups of a value.
    /// </summary>
    /// <param name="value">The value to hash.</param>
    /// <param name="cs0">The case-sensitive hash of the first eight bytes.</param>
    /// <param name="uc0">The case-insensitive hash of the first eight bytes.</param>
    /// <param name="cs1">The case-sensitive hash of the next eight bytes, or zero if the value is not that long.</param>
    /// <param name="uc1">The case-insensitive hash of the next eight bytes, or zero if the value is not that long.</param>
    public static void Hash(scoped ReadOnlySpan<byte> value, out long cs0, out long uc0, out long cs1, out long uc1)
    {
        Hash(value, out cs0, out uc0);
        if (value.Length > MaxBytesHashed)
        {
            Hash(value.Slice(start: MaxBytesHashed), out cs1, out uc1);
        }
        else
        {
            cs1 = uc1 = 0;
        }
    }

    /// <summary>
    /// Computes the case-sensitive and case-insensitive hashes of the first and second
    /// eight-character groups of a value.
    /// </summary>
    /// <param name="value">The value to hash.</param>
    /// <param name="cs0">The case-sensitive hash of the first eight characters.</param>
    /// <param name="uc0">The case-insensitive hash of the first eight characters.</param>
    /// <param name="cs1">The case-sensitive hash of the next eight characters, or zero if the value is not that long.</param>
    /// <param name="uc1">The case-insensitive hash of the next eight characters, or zero if the value is not that long.</param>
    public static void Hash(scoped ReadOnlySpan<char> value, out long cs0, out long uc0, out long cs1, out long uc1)
    {
        Hash(value, out cs0, out uc0);
        if (value.Length > MaxBytesHashed)
        {
            Hash(value.Slice(start: MaxBytesHashed), out cs1, out uc1);
        }
        else
        {
            cs1 = uc1 = 0;
        }
    }
}
