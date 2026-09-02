using System;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace CodeBrix.Redis.Respite; //was previously: RESPite;

public readonly partial struct AsciiHash : IEquatable<AsciiHash>,
    ISpanFormattable
{
    // ReSharper disable InconsistentNaming
    private readonly long _hashCS, _hashUC;
    // ReSharper restore InconsistentNaming
    private readonly int _index, _length;
    private readonly byte[] _arr;

    /// <summary>
    /// The length of this value, in bytes.
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// The optimal buffer length (with padding) to use for this value.
    /// </summary>
    public int BufferLength => (Length + 1 + 7) & ~7; // an extra byte, then round up to word-size

    /// <summary>
    /// The bytes of this value.
    /// </summary>
    public ReadOnlySpan<byte> Span => new(_arr ?? [], _index, _length);

    /// <summary>
    /// Indicates whether this value has no bytes; this is also true of a <c>default</c> instance.
    /// </summary>
    public bool IsEmpty => Length == 0;

    /// <summary>
    /// Creates a value from a span, taking a copy of the bytes.
    /// </summary>
    /// <param name="value">The bytes to copy.</param>
    public AsciiHash(ReadOnlySpan<byte> value) : this(value.ToArray(), 0, value.Length) { }

    /// <summary>
    /// Creates a value from a string, encoded as ASCII.
    /// </summary>
    /// <param name="value">The text to encode; <c>null</c> gives an empty value.</param>
    public AsciiHash(string? value) : this(value is null ? [] : Encoding.ASCII.GetBytes(value)) { }

    /// <inheritdoc/>
    public override int GetHashCode() => _hashCS.GetHashCode();

    /// <inheritdoc/>
    public override string ToString() => _length == 0 ? "" : Encoding.ASCII.GetString(_arr, _index, _length);

    /// <inheritdoc/>
    public override bool Equals(object? other) => other is AsciiHash hash && Equals(hash);

    /// <inheritdoc cref="Equals(object)" />
    [CLSCompliant(false)]
    public bool Equals(in AsciiHash other)
    {
        return (_length == other.Length & _hashCS == other._hashCS)
               && (_length <= MaxBytesHashed || Span.SequenceEqual(other.Span));
    }

    bool IEquatable<AsciiHash>.Equals(AsciiHash other) => Equals(other);

    /// <summary>
    /// Creates a value over an entire array, without copying it.
    /// </summary>
    /// <param name="arr">The backing array; <c>null</c> gives an empty value.</param>
    public AsciiHash(byte[] arr) : this(arr, 0, -1) { }

    /// <summary>
    /// Creates a value over part of an array, without copying it.
    /// </summary>
    /// <param name="arr">The backing array; <c>null</c> gives an empty value.</param>
    /// <param name="index">The offset of the first byte of the value.</param>
    /// <param name="length">The length of the value, or a negative value for "the rest of the array".</param>
    public AsciiHash(byte[] arr, int index, int length)
    {
        _arr = arr ?? [];
        _index = index;
        _length = length < 0 ? (_arr.Length - index) : length;

        var span = new ReadOnlySpan<byte>(_arr, _index, _length);
        Hash(span, out _hashCS, out _hashUC);
    }

    /// <summary>
    /// Tests whether this value equals the supplied bytes, case-sensitively.
    /// </summary>
    /// <param name="value">The bytes to compare against.</param>
    /// <returns><c>true</c> if the two are the same length and hold the same bytes.</returns>
    public bool IsCS(ReadOnlySpan<byte> value)
    {
        var cs = HashCS(value);
        var len = _length;
        if (cs != _hashCS | value.Length != len) return false;
        return len <= MaxBytesHashed || Span.SequenceEqual(value);
    }

    /// <summary>
    /// Tests whether this value equals the supplied bytes, ignoring ASCII case.
    /// </summary>
    /// <param name="value">The bytes to compare against.</param>
    /// <returns><c>true</c> if the two match, ignoring ASCII case.</returns>
    public bool IsCI(ReadOnlySpan<byte> value)
    {
        var uc = HashUC(value);
        var len = _length;
        if (uc != _hashUC | value.Length != len) return false;
        return len <= MaxBytesHashed || SequenceEqualsCI(Span, value);
    }

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) => ToString();

    bool ISpanFormattable.TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
    {
        charsWritten = 0;
        var source = Span;
        if (source.IsEmpty) return true;
        if (source.Length > destination.Length) return false;

        charsWritten = Encoding.ASCII.GetChars(source, destination);
        return true;
    }
}
