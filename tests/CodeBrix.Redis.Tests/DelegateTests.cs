using System;
using System.Collections.Generic;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class DelegateTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(25)]
    public void foo(int count)
    {
        Delegates.IsSupported.Should().BeTrue();
        Action? action = null;
        MulticastDelegate? m = action;
        List<int> captured = [];
        for (int i = 0; i < count; i++)
        {
            action += Add(captured, i);
            static Action Add(List<int> captured, int i) => () => captured.Add(i);
        }

        switch (count)
        {
            case 0:
            action.Should().BeNull();
            break;
            case 1:
            Assert.NotNull(action);
            action.IsSingle().Should().BeTrue();
            break;
            default:
            Assert.NotNull(action);
            action.IsSingle().Should().BeFalse();
            break;
        }

        int foreachCount = 0;
        foreach (var inner in action.AsEnumerable())
        {
            inner.Invoke();
            foreachCount++;
        }
        foreachCount.Should().Be(count);
        captured.Count.Should().Be(count);
        for (int i = 0; i < captured.Count; i++)
        {
            captured[i].Should().Be(i);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(25)]
    public void matches_get_invocation_list(int count)
    {
        Action? action = null;
        for (int i = 0; i < count; i++)
        {
            action += Noop;
        }

        Assert.NotNull(action);
        var expected = action.GetInvocationList();
        expected.Length.Should().Be(count);
        action.IsSingle().Should().Be(count == 1);

        int index = 0;
        foreach (var inner in action.AsEnumerable())
        {
            inner.Should().BeSameAs((Action)expected[index++]);
        }
        index.Should().Be(count);

        // and again, to check that removal keeps things consistent
        action -= Noop;
        if (count == 1)
        {
            action.Should().BeNull();
            return;
        }

        Assert.NotNull(action);
        expected = action.GetInvocationList();
        expected.Length.Should().Be(count - 1);
        action.IsSingle().Should().Be(count == 2);
        index = 0;
        foreach (var inner in action.AsEnumerable())
        {
            inner.Should().BeSameAs((Action)expected[index++]);
        }
        index.Should().Be(count - 1);

        static void Noop() { }
    }

    [Fact]
    public void reset_repeats_sequence()
    {
        Action? action = Noop;
        action += Noop;

        var iterator = action.GetEnumerator();
        iterator.MoveNext().Should().BeTrue();
        iterator.MoveNext().Should().BeTrue();
        iterator.MoveNext().Should().BeFalse();

        iterator.Reset();
        int count = 0;
        while (iterator.MoveNext()) count++;
        count.Should().Be(2);

        static void Noop() { }
    }
}
