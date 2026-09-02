using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace CodeBrix.Redis.Respite.Messages; //was previously: RESPite.Messages;

public ref partial struct RespReader
{
    /// <summary>
     /// Reads the sub-elements associated with an aggregate value. For convenience, when
     /// using <c>foreach</c> (<see cref="MoveNext()"/>) the reader
     /// is advanced into the child element ready for reading, which bypasses attributes. If attributes
     /// are required from child elements, the iterator can be advanced manually (not via
     /// <c>foreach</c> using an optional attribute-reader in the <see cref="MoveNext()"/> call.
     /// </summary>
    public readonly AggregateEnumerator AggregateChildren() => new(in this);

    /// <summary>
    /// Reads the sub-elements associated with an aggregate value.
    /// </summary>
    public ref struct AggregateEnumerator
    {
        // Note that _reader is the overall reader that can see outside this aggregate, as opposed
        // to Current which is the sub-tree of the current element *only*
        private RespReader _reader;
        private int _remaining;

        /// <summary>
        /// Create a new enumerator for the specified <paramref name="reader"/>.
        /// </summary>
        /// <param name="reader">The reader containing the data for this operation.</param>
        public AggregateEnumerator(scoped in RespReader reader)
        {
            reader.DemandAggregate();
            _remaining = reader.IsStreaming ? -1 : reader._length;
            _reader = reader;
            Value = default;
        }

        /// <inheritdoc cref="IEnumerable{T}.GetEnumerator()"/>
        public readonly AggregateEnumerator GetEnumerator() => this;

        /// <inheritdoc cref="IEnumerator{T}.Current"/>
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
#if DEBUG
        [Experimental("SERDBG", Message = $"Prefer {nameof(Value)}")]
#endif
        public RespReader Current => Value;

        /// <summary>
        /// Gets the current element associated with this reader.
        /// </summary>
        public RespReader Value; // intentionally a field, because of ref-semantics

        /// <summary>
        /// Move to the next child if possible, and move the child element into the next node.
        /// </summary>
        public bool MoveNext(RespPrefix prefix)
        {
            bool result = MoveNextRaw();
            if (result)
            {
                Value.MoveNext(prefix);
            }
            return result;
        }

        /// <summary>
        /// Move to the next child if possible, and move the child element into the next node.
        /// </summary>
        /// <typeparam name="T">The type of data represented by this reader.</typeparam>
        public bool MoveNext<T>(RespPrefix prefix, RespAttributeReader<T> respAttributeReader, ref T attributes)
        {
            bool result = MoveNextRaw(respAttributeReader, ref attributes);
            if (result)
            {
                Value.MoveNext(prefix);
            }
            return result;
        }

        /// <summary>
        /// Move to the next child and leave the reader *ahead of* the first element,
        /// allowing us to read attribute data.
        /// </summary>
        /// <remarks>If you are not consuming attribute data, <see cref="MoveNext()"/> is preferred.</remarks>
        public bool MoveNextRaw()
        {
            object? attributes = null;
            return MoveNextCore(null, ref attributes);
        }

        /// <summary>
        /// Move to the next child and move into the first element (skipping attributes etc), leaving it ready to consume.
        /// </summary>
        public bool MoveNext()
        {
            object? attributes = null;
            if (MoveNextCore(null, ref attributes))
            {
                Value.MoveNext();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Move to the next child (capturing attribute data) and leave the reader *ahead of* the first element,
        /// allowing us to also read attribute data of the child.
        /// </summary>
        /// <typeparam name="T">The type of attribute data represented by this reader.</typeparam>
        /// <remarks>If you are not consuming attribute data, <see cref="MoveNext()"/> is preferred.</remarks>
        public bool MoveNextRaw<T>(RespAttributeReader<T> respAttributeReader, ref T attributes)
            => MoveNextCore<T>(respAttributeReader, ref attributes);

        /// <inheritdoc cref="IEnumerator.MoveNext()"/>>
        private bool MoveNextCore<T>(RespAttributeReader<T>? attributeReader, ref T attributes)
        {
            if (_remaining == 0)
            {
                Value = default;
                return false;
            }

            // in order to provide access to attributes etc, we want Current to be positioned
            // *before* the next element; for that, we'll take a snapshot before we read
            _reader.MovePastCurrent();
            var snapshot = _reader.Clone();

            if (!(attributeReader is null
                    ? _reader.TryReadNextSkipAttributes(skipStreamTerminator: false)
                    : _reader.TryReadNextProcessAttributes(attributeReader, ref attributes, false)))
            {
                if (_remaining != 0) ThrowEof(); // incomplete aggregate, simple or streaming
                _remaining = 0;
                Value = default;
                return false;
            }

            if (_remaining > 0)
            {
                // non-streaming, decrement
                _remaining--;
            }
            else if (_reader.Prefix == RespPrefix.StreamTerminator)
            {
                // end of streaming aggregate
                _remaining = 0;
                Value = default;
                return false;
            }

            // move past that sub-tree and trim the "snapshot" state, giving
            // us a scoped reader that is *just* that sub-tree
            _reader.SkipChildren();
            snapshot.TrimToTotal(_reader.BytesConsumed);

            Value = snapshot;
            return true;
        }

        /// <summary>
        /// Move to the end of this aggregate and export the state of the <paramref name="reader"/>.
        /// </summary>
        /// <param name="reader">The reader positioned at the end of the data; this is commonly
        /// used to update a tree reader, to get to the next data after the aggregate.</param>
        public void MovePast(out RespReader reader)
        {
            while (MoveNextRaw()) { }
            reader = _reader;
        }

        /// <summary>
        /// Moves to the next element, and moves into that element (skipping attributes etc), leaving it ready to consume.
        /// </summary>
        public void DemandNext()
        {
            if (!MoveNext()) ThrowEof();
        }

        /// <summary>
        /// Moves to the next child and projects it into a value.
        /// </summary>
        /// <typeparam name="T">The type of data to be projected.</typeparam>
        /// <param name="projection">The projection applied to the child.</param>
        /// <returns>The projected value.</returns>
        /// <exception cref="System.IO.EndOfStreamException">There is no next child.</exception>
        public T ReadOne<T>(Projection<T> projection)
        {
            DemandNext();
            return projection(ref Value);
        }

        /// <summary>
        /// Projects the next <c>target.Length</c> children into <paramref name="target"/>.
        /// </summary>
        /// <typeparam name="TResult">The type of data to be projected.</typeparam>
        /// <param name="target">The span to fill.</param>
        /// <param name="projection">The projection applied to each child.</param>
        /// <exception cref="System.IO.EndOfStreamException">The aggregate ran out of children.</exception>
        public void FillAll<TResult>(scoped Span<TResult> target, Projection<TResult> projection)
        {
            FillAll(target, ref projection, static (ref projection, ref reader) => projection(ref reader));
        }

        /// <summary>
        /// Projects the next <c>target.Length</c> children into <paramref name="target"/>, passing
        /// caller state to the projection.
        /// </summary>
        /// <typeparam name="TState">Additional state required by the projection.</typeparam>
        /// <typeparam name="TResult">The type of data to be projected.</typeparam>
        /// <param name="target">The span to fill.</param>
        /// <param name="state">The caller's state, passed by reference to each projection call.</param>
        /// <param name="projection">The projection applied to each child.</param>
        /// <exception cref="System.IO.EndOfStreamException">The aggregate ran out of children.</exception>
        public void FillAll<TState, TResult>(scoped Span<TResult> target, ref TState state, Projection<TState, TResult> projection)
            where TState : allows ref struct
        {
            for (int i = 0; i < target.Length; i++)
            {
                DemandNext();
                target[i] = projection(ref state, ref Value);
            }
        }

        /// <summary>
        /// Reads the children in pairs, projecting each pair into one element of <paramref name="target"/>;
        /// this consumes two children per element.
        /// </summary>
        /// <typeparam name="TFirst">The type projected from the first child of each pair.</typeparam>
        /// <typeparam name="TSecond">The type projected from the second child of each pair.</typeparam>
        /// <typeparam name="TResult">The type of data to be projected.</typeparam>
        /// <param name="target">The span to fill.</param>
        /// <param name="first">The projection applied to the first child of each pair.</param>
        /// <param name="second">The projection applied to the second child of each pair.</param>
        /// <param name="combine">Combines the two projected values into the result element.</param>
        /// <exception cref="System.IO.EndOfStreamException">The aggregate ran out of children.</exception>
        public void FillAll<TFirst, TSecond, TResult>(
            scoped Span<TResult> target,
            Projection<TFirst> first,
            Projection<TSecond> second,
            Func<TFirst, TSecond, TResult> combine)
        {
            for (int i = 0; i < target.Length; i++)
            {
                DemandNext();

                var x = first(ref Value);

                DemandNext();

                var y = second(ref Value);
                target[i] = combine(x, y);
            }
        }

        /// <summary>
        /// Reads the children in pairs, projecting each pair into one element of <paramref name="target"/>
        /// with caller state; this consumes two children per element.
        /// </summary>
        /// <typeparam name="TState">Additional state required by the projections.</typeparam>
        /// <typeparam name="TFirst">The type projected from the first child of each pair.</typeparam>
        /// <typeparam name="TSecond">The type projected from the second child of each pair.</typeparam>
        /// <typeparam name="TResult">The type of data to be projected.</typeparam>
        /// <param name="target">The span to fill.</param>
        /// <param name="state">The caller's state, passed by reference to each projection call.</param>
        /// <param name="first">The projection applied to the first child of each pair.</param>
        /// <param name="second">The projection applied to the second child of each pair.</param>
        /// <param name="combine">Combines the caller state and the two projected values into the result element.</param>
        /// <exception cref="System.IO.EndOfStreamException">The aggregate ran out of children.</exception>
        public void FillAll<TState, TFirst, TSecond, TResult>(
            scoped Span<TResult> target,
            ref TState state,
            Projection<TState, TFirst> first,
            Projection<TState, TSecond> second,
            Func<TState, TFirst, TSecond, TResult> combine)
            where TState : allows ref struct
        {
            for (int i = 0; i < target.Length; i++)
            {
                DemandNext();

                var x = first(ref state, ref Value);

                DemandNext();

                var y = second(ref state, ref Value);
                target[i] = combine(state, x, y);
            }
        }
    }

    internal void TrimToTotal(long length) => TrimToRemaining(length - BytesConsumed);

    internal void TrimToRemaining(long bytes)
    {
        if (_prefix != RespPrefix.None || bytes < 0) Throw();

        var current = CurrentAvailable;
        if (bytes <= current)
        {
            UnsafeTrimCurrentBy(current - (int)bytes);
            _remainingTailLength = 0;
            return;
        }

        bytes -= current;
        if (bytes <= _remainingTailLength)
        {
            _remainingTailLength = bytes;
            return;
        }

        Throw();
        static void Throw() => throw new ArgumentOutOfRangeException(nameof(bytes));
    }
}
