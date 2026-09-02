using System;
using System.Collections.Generic;

namespace CodeBrix.Redis.Respite; //was previously: RESPite;

public readonly partial struct AsciiHash
{
    /// <summary>
    /// An equality comparer that treats two values as equal when they hold exactly the same bytes.
    /// </summary>
    public static IEqualityComparer<AsciiHash> CaseSensitiveEqualityComparer => CaseSensitiveComparer.Instance;

    /// <summary>
    /// An equality comparer that treats two values as equal when they match ignoring ASCII case.
    /// </summary>
    public static IEqualityComparer<AsciiHash> CaseInsensitiveEqualityComparer => CaseInsensitiveComparer.Instance;

    private sealed class CaseSensitiveComparer : IEqualityComparer<AsciiHash>
    {
        private CaseSensitiveComparer() { }
        public static readonly CaseSensitiveComparer Instance = new();

        public bool Equals(AsciiHash x, AsciiHash y)
        {
            var len = x.Length;
            return (len == y.Length & x._hashCS == y._hashCS)
                   && (len <= MaxBytesHashed || x.Span.SequenceEqual(y.Span));
        }

        public int GetHashCode(AsciiHash obj) => obj._hashCS.GetHashCode();
    }

    private sealed class CaseInsensitiveComparer : IEqualityComparer<AsciiHash>
    {
        private CaseInsensitiveComparer() { }
        public static readonly CaseInsensitiveComparer Instance = new();

        public bool Equals(AsciiHash x, AsciiHash y)
        {
            var len = x.Length;
            return (len == y.Length & x._hashUC == y._hashUC)
                   && (len <= MaxBytesHashed || SequenceEqualsCI(x.Span, y.Span));
        }

        public int GetHashCode(AsciiHash obj) => obj._hashUC.GetHashCode();
    }
}
