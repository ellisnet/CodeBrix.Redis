using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using CodeBrix.Redis.Respite.Buffers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Respite.Tests; //was previously: RESPite.Tests;

public class CycleBufferTests
{
    [Fact]
    public void write_multi_segment_sequence_writes_every_segment()
    {
        //Arrange
        // three segments, each with distinct content and differing lengths; a regression guard against
        // writing the first segment repeatedly instead of walking each segment
        var expected = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
        var seq = CreateSequence(new byte[] { 0, 1, 2 }, new byte[] { 3, 4, 5, 6 }, new byte[] { 7, 8 });
        seq.IsSingleSegment.Should().BeFalse();

        //Act
        var buffer = CycleBuffer.Create();
        buffer.Write(in seq);

        //Assert
        buffer.GetCommittedLength().Should().Be(expected.Length);
        buffer.GetAllCommitted().ToArray().Should().Equal(expected);
    }

    private static ReadOnlySequence<byte> CreateSequence(params byte[][] chunks)
    {
        Segment? head = null, tail = null;
        long runningIndex = 0;
        foreach (var chunk in chunks)
        {
            var next = new Segment(chunk, runningIndex);
            if (tail is null) head = next;
            else tail.SetNext(next);
            tail = next;
            runningIndex += chunk.Length;
        }
        return new ReadOnlySequence<byte>(head!, 0, tail!, tail!.Memory.Length);
    }

    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        public Segment(ReadOnlyMemory<byte> memory, long runningIndex)
        {
            Memory = memory;
            RunningIndex = runningIndex;
        }

        public void SetNext(Segment next) => Next = next;
    }

    public enum Timing
    {
        CommitEverythingBeforeDiscard,
        CommitAfterFirstDiscard,
    }

    [Theory]
    [InlineData(Timing.CommitEverythingBeforeDiscard)]
    [InlineData(Timing.CommitAfterFirstDiscard)]
    public void can_discard_safely(Timing timing)
    {
        //Arrange
        var buffer = CycleBuffer.Create();
        buffer.GetUncommittedSpan(10).Slice(0, 10).Fill(1);
        buffer.GetCommittedLength().Should().Be(0);
        buffer.Commit(10);
        buffer.GetCommittedLength().Should().Be(10);
        buffer.GetUncommittedSpan(15).Slice(0, 15).Fill(2);

        //Act
        if (timing is Timing.CommitEverythingBeforeDiscard) buffer.Commit(15);

        //Assert
        buffer.TryGetFirstCommittedSpan(1, out var committed).Should().BeTrue();
        switch (timing)
        {
            case Timing.CommitEverythingBeforeDiscard:
                committed.Length.Should().Be(25);
                for (int i = 0; i < 10; i++)
                {
                    committed[i].Should().Be(1, "committed[{0}] should hold the first write", i);
                }
                for (int i = 10; i < 25; i++)
                {
                    committed[i].Should().Be(2, "committed[{0}] should hold the second write", i);
                }
                break;
            case Timing.CommitAfterFirstDiscard:
                committed.Length.Should().Be(10);
                for (int i = 0; i < committed.Length; i++)
                {
                    committed[i].Should().Be(1, "committed[{0}] should hold the first write", i);
                }
                break;
        }

        buffer.DiscardCommitted(committed.Length);
        buffer.GetCommittedLength().Should().Be(0);

        // now (simulating concurrent) we commit the second span
        if (timing is Timing.CommitAfterFirstDiscard)
        {
            buffer.Commit(15);

            buffer.GetCommittedLength().Should().Be(15);

            // and we should be able to read those bytes
            buffer.TryGetFirstCommittedSpan(1, out committed).Should().BeTrue();
            committed.Length.Should().Be(15);
            for (int i = 0; i < committed.Length; i++)
            {
                committed[i].Should().Be(2, "committed[{0}] should hold the second write", i);
            }

            buffer.DiscardCommitted(committed.Length);
        }

        buffer.GetCommittedLength().Should().Be(0);
    }
}
