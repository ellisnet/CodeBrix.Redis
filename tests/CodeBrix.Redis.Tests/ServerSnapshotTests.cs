using System;
using System.Linq;
using System.Runtime.CompilerServices;
using SilverAssertions;
using Xunit;
using static CodeBrix.Redis.ConnectionMultiplexer;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class ServerSnapshotTests
{
    [Fact]
    public void empty_behaviour()
    {
        //Act
        var snapshot = ServerSnapshot.Empty;

        //Assert
        snapshot.Add(null!).Should().BeSameAs(snapshot);

        snapshot.Count.Should().Be(0);
        ManualCount(snapshot).Should().Be(0);
        ManualCount(snapshot, static _ => true).Should().Be(0);
        ManualCount(snapshot, static _ => false).Should().Be(0);

        Enumerable.Count(snapshot).Should().Be(0);
        Enumerable.Count(snapshot, static _ => true).Should().Be(0);
        Enumerable.Count(snapshot, static _ => false).Should().Be(0);

        Enumerable.Any(snapshot).Should().BeFalse();
        snapshot.Any().Should().BeFalse();

        Enumerable.Any(snapshot, static _ => true).Should().BeFalse();
        snapshot.Any(static _ => true).Should().BeFalse();
        Enumerable.Any(snapshot, static _ => false).Should().BeFalse();
        snapshot.Any(static _ => false).Should().BeFalse();

        snapshot.Should().BeEmpty();
        Enumerable.Where(snapshot, static _ => true).Should().BeEmpty();
        snapshot.Where(static _ => true).Should().BeEmpty();
        Enumerable.Where(snapshot, static _ => false).Should().BeEmpty();
        snapshot.Where(static _ => false).Should().BeEmpty();

        snapshot.Where(CommandFlags.DemandMaster).Should().BeEmpty();
        snapshot.Where(CommandFlags.DemandReplica).Should().BeEmpty();
        snapshot.Where(CommandFlags.None).Should().BeEmpty();
        snapshot.Where(CommandFlags.FireAndForget | CommandFlags.NoRedirect | CommandFlags.NoScriptCache).Should().BeEmpty();
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(5, 0)]
    [InlineData(5, 3)]
    [InlineData(5, 5)]
    public void non_empty_behaviour(int count, int replicaCount)
    {
        var snapshot = ServerSnapshot.Empty;
        for (int i = 0; i < count; i++)
        {
            //was previously: FormatterServices.GetSafeUninitializedObject - obsolete (SYSLIB0050);
            //RuntimeHelpers.GetUninitializedObject is its in-box, non-obsolete equivalent.
            var dummy = (ServerEndPoint)RuntimeHelpers.GetUninitializedObject(typeof(ServerEndPoint));
            dummy.IsReplica = i < replicaCount;
            snapshot = snapshot.Add(dummy);
        }

        snapshot.Count.Should().Be(count);
        ManualCount(snapshot).Should().Be(count);
        ManualCount(snapshot, static _ => true).Should().Be(count);
        ManualCount(snapshot, static _ => false).Should().Be(0);
        ManualCount(snapshot, static s => s.IsReplica).Should().Be(replicaCount);

        Enumerable.Count(snapshot).Should().Be(count);
        Enumerable.Count(snapshot, static _ => true).Should().Be(count);
        Enumerable.Count(snapshot, static _ => false).Should().Be(0);
        Enumerable.Count(snapshot, static s => s.IsReplica).Should().Be(replicaCount);

        Enumerable.Any(snapshot).Should().BeTrue();
        snapshot.Any().Should().BeTrue();

        Enumerable.Any(snapshot, static _ => true).Should().BeTrue();
        snapshot.Any(static _ => true).Should().BeTrue();
        Enumerable.Any(snapshot, static _ => false).Should().BeFalse();
        snapshot.Any(static _ => false).Should().BeFalse();

        snapshot.Should().NotBeEmpty();
        Enumerable.Where(snapshot, static _ => true).Should().NotBeEmpty();
        snapshot.Where(static _ => true).Should().NotBeEmpty();
        Enumerable.Where(snapshot, static _ => false).Should().BeEmpty();
        snapshot.Where(static _ => false).Should().BeEmpty();

        snapshot.Where(CommandFlags.DemandMaster).Count().Should().Be(snapshot.Count - replicaCount);
        snapshot.Where(CommandFlags.DemandReplica).Count().Should().Be(replicaCount);
        snapshot.Where(CommandFlags.None).Count().Should().Be(snapshot.Count);
        snapshot.Where(CommandFlags.FireAndForget | CommandFlags.NoRedirect | CommandFlags.NoScriptCache).Count().Should().Be(snapshot.Count);
    }

    private static int ManualCount(ServerSnapshot snapshot, Func<ServerEndPoint, bool>? predicate = null)
    {
        // ^^^ tests the custom iterator implementation
        int count = 0;
        if (predicate is null)
        {
            foreach (var item in snapshot)
            {
                count++;
            }
        }
        else
        {
            foreach (var item in snapshot.Where(predicate))
            {
                count++;
            }
        }
        return count;
    }
}
