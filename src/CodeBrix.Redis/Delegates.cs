// .NET 9 added Delegate.EnumerateInvocationList, which is exactly the allocation-free
// enumerator we want, is runtime-agnostic, and works on NativeAOT; prefer it.
//was previously: that preference was a file-local `#define BCL_INVOCATION_LIST` under
//`#if NET9_0_OR_GREATER`, with `#define UNSAFE_ACCESSOR` and a reflection-emit fallback
//for older targets. net10.0 always takes the BCL path, so both were resolved away.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CodeBrix.Redis; //was previously: StackExchange.Redis;

/// <summary>
/// Provides utility methods for working *efficiently* with multicast delegates.
/// </summary>
internal static class Delegates
{
    /// <summary>
    /// Iterate over the individual elements of a multicast delegate (without allocation).
    /// </summary>
    /// <typeparam name="T">The type of delegate being enumerated.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DelegateEnumerator<T> GetEnumerator<T>(this T? handler) where T : MulticastDelegate
        => new(handler);

    /// <summary>
    /// Iterate over the individual elements of a multicast delegate (without allocation).
    /// </summary>
    /// <typeparam name="T">The type of delegate being enumerated.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DelegateEnumerable<T> AsEnumerable<T>(this T? handler) where T : MulticastDelegate
        => new(handler);

    /// <summary>
    /// Indicates whether a particular delegate is known to be a single-target delegate.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSingle(this MulticastDelegate handler)
    {
        var iterator = Delegate.EnumerateInvocationList(handler);
        return iterator.MoveNext() && !iterator.MoveNext();
    }

    /// <summary>
    /// Indicates whether optimized usage is supported on this environment; without this, it may still
    /// work, but with additional overheads at runtime.
    /// </summary>
    public static bool IsSupported => s_isAvailable;

    private const bool s_isAvailable = true;

    /// <summary>
    /// Allows allocation-free enumerator over the individual elements of a multicast delegate.
    /// </summary>
    /// <typeparam name="T">The type of delegate being enumerated.</typeparam>
    public readonly struct DelegateEnumerable<T> : IEnumerable<T> where T : MulticastDelegate
    {
        private readonly T? _handler;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal DelegateEnumerable(T? handler) => _handler = handler;

        /// <summary>
        /// Iterate over the individual elements of a multicast delegate (without allocation).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DelegateEnumerator<T> GetEnumerator() => new(_handler);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// Allows allocation-free enumerator over the individual elements of a multicast delegate.
    /// </summary>
    /// <typeparam name="T">The type of delegate being enumerated.</typeparam>
    public struct DelegateEnumerator<T> : IEnumerator<T> where T : MulticastDelegate
    {
        private readonly T? _handler;
        private Delegate.InvocationListEnumerator<T> _iterator;

        internal DelegateEnumerator(T? handler)
        {
            _handler = handler;
            _iterator = Delegate.EnumerateInvocationList(handler);
        }

        /// <summary>
        /// Provides the current value of the sequence.
        /// </summary>
        public T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _iterator.Current;
        }

        object? IEnumerator.Current => Current;

        void IDisposable.Dispose() { }

        /// <summary>
        /// Move to the next item in the sequence.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            return _iterator.MoveNext();
        }

        /// <summary>
        /// Reset the enumerator, allowing the sequence to be repeated.
        /// </summary>
        public void Reset()
        {
            _iterator = Delegate.EnumerateInvocationList(_handler);
        }
    }
}
