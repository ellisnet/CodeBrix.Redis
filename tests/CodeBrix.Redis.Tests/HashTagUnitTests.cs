using System;
using System.Collections.Generic;
using System.Text;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class HashTagUnitTests
{
    [Fact]
    public void test_hash_tag_coverage()
    {
        HashSet<string> uniques = [];
        ServerSelectionStrategy.GetHashTag(ServerSelectionStrategy.NoSlot).Should().Be("");
        ServerSelectionStrategy.GetHashTag(ServerSelectionStrategy.MultipleSlots).Should().Be("");
        Span<byte> buffer = stackalloc byte[3];
        for (int i = 0; i < ServerSelectionStrategy.TotalSlots; i++)
        {
            var tag = ServerSelectionStrategy.GetHashTag(i);
            string.IsNullOrEmpty(tag).Should().BeFalse();
            uniques.Add(tag).Should().BeTrue();

            var len = Encoding.ASCII.GetBytes(tag, buffer);
            var slot = ServerSelectionStrategy.GetClusterSlot(buffer.Slice(0, len));
            slot.Should().Be(i);
        }
        uniques.Count.Should().Be(ServerSelectionStrategy.TotalSlots);
    }
}
