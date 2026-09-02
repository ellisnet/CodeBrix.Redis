using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

/// <summary>
/// <c>CLUSTER SLOTS</c> against a real cluster; the naming-configuration matrix lives in
/// <see cref="ClusterSlotsUnitTests"/>. Runs per protocol, since the metadata element is a map under RESP3
/// and a flat array under RESP2.
/// </summary>
[RunPerProtocol]
public class ClusterSlotsTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    protected override string GetConfiguration() => TestConfig.Current.ClusterServersAndPorts + ",connectTimeout=10000";

    private static readonly Version NodeIdVersion = new(4, 0, 0);

    [Fact]
    public async Task assignments_cover_the_whole_keyspace()
    {
        await using var conn = Create(allowAdmin: true);
        var result = await conn.GetServer(conn.GetEndPoints()[0]).ClusterSlotsAsync();
        Assert.NotNull(result);
        result.Assignments.Should().NotBeEmpty();

        var covered = result.Assignments.Sum(x => x.Slots.To - x.Slots.From + 1);
        Log($"{result.Assignments.Count} assignments covering {covered} slots");
        covered.Should().Be(SlotRange.MaxSlot - SlotRange.MinSlot + 1);
    }

    [Fact]
    public async Task every_primary_is_addressable_and_identified()
    {
        await using var conn = Create(allowAdmin: true);
        var api = conn.GetServer(conn.GetEndPoints()[0]);
        Assert.SkipUnless(api.Version >= NodeIdVersion, $"node ids need {NodeIdVersion}, server is {api.Version}");

        var result = await api.ClusterSlotsAsync();
        Assert.NotNull(result);
        foreach (var assignment in result.Assignments)
        {
            foreach (var node in new[] { assignment.Primary }.Concat(assignment.Replicas))
            {
                // an unconfigured cluster prefers addresses, so every node should be usable as-is
                Assert.NotNull(node.EndPoint);
                string.IsNullOrEmpty(node.NodeId).Should().BeFalse();
                node.Port.Should().BeInRange(1, ushort.MaxValue);
            }
        }
    }

    [Fact]
    public async Task replicas_are_reported()
    {
        await using var conn = Create(allowAdmin: true);
        var result = await conn.GetServer(conn.GetEndPoints()[0]).ClusterSlotsAsync();
        Assert.NotNull(result);
        // whether replicas exist at all is a property of the deployment, not of the parser - the Windows CI
        // fleet is smaller than the local compose - so skip rather than fail where there are none
        var withReplicas = result.Assignments.Count(x => x.Replicas.Count > 0);
        Log($"{withReplicas} of {result.Assignments.Count} assignments report replicas");
        Assert.SkipWhen(withReplicas == 0, "this deployment reports no replicas");

        var replica = result.Assignments.First(x => x.Replicas.Count > 0).Replicas[0];
        Assert.NotNull(replica.EndPoint);
        replica.NodeId.Should().NotBe(result.Assignments.First(x => x.Replicas.Count > 0).Primary.NodeId);
    }

    [Fact]
    public async Task export_includes_both_cluster_views()
    {
        var path = Path.Combine(Path.GetTempPath(), $"se-redis-export-{Guid.NewGuid():n}.zip");
        try
        {
            await using (var conn = Create(allowAdmin: true))
            using (var file = File.Create(path))
            {
                conn.ExportConfiguration(file, ExportOptions.Cluster);
            }

            using var zip = ZipFile.OpenRead(path);
            var names = zip.Entries.Select(x => x.FullName).ToArray();
            Log(string.Join(", ", names));

            // the export is per-server, so there should be one of each alongside every other
            var nodeFiles = names.Count(x => x.EndsWith("/nodes.txt", StringComparison.Ordinal));
            var slotFiles = names.Count(x => x.EndsWith("/slots.txt", StringComparison.Ordinal));
            nodeFiles.Should().NotBe(0);
            slotFiles.Should().Be(nodeFiles);

            var slots = zip.Entries.First(x => x.FullName.EndsWith("/slots.txt", StringComparison.Ordinal));
            using var reader = new StreamReader(slots.Open());
            var content = await reader.ReadToEndAsync();
            Log(content);

            // one line per node per range, endpoint reported exactly as the server gave it
            content.Should().Contain(" primary endpoint=");
            content.Should().Contain(" id=");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task shadow_topology_agrees_with_nodes_per_node()
    {
        // the live cluster has heavily fragmented ownership, so this compares real range boundaries rather
        // than the toy server's tidy ones - the strongest check available before the slot map is switched over
        await using var conn = Create(allowAdmin: true);
        var endpoint = conn.GetEndPoints()[0];
        var api = conn.GetServer(endpoint);
        Assert.SkipUnless(api.Version >= NodeIdVersion, $"node ids need {NodeIdVersion}, server is {api.Version}");

        var nodes = await api.ClusterNodesAsync();
        Assert.NotNull(nodes);
        var topology = ClusterTopology.From(await api.ClusterSlotsAsync());
        Assert.NotNull(topology);
        int compared = 0;
        foreach (var node in topology.Nodes.Where(x => !x.IsReplica))
        {
            var expected = Slots(nodes.Nodes.Single(x => x.NodeId == node.NodeId).Slots);
            var actual = Slots(node.Slots);
            Log($"{node.NodeId}: {actual.Length} slots over {node.Slots.Count} ranges");
            actual.Should().Equal(expected);
            compared++;
        }
        compared.Should().NotBe(0);

        static int[] Slots(System.Collections.Generic.IEnumerable<SlotRange> ranges)
            => ranges.SelectMany(r => Enumerable.Range(r.From, r.To - r.From + 1)).OrderBy(x => x).ToArray();
    }

    [Fact]
    public async Task slots_and_nodes_agree_on_primaries()
    {
        await using var conn = Create(allowAdmin: true);
        var api = conn.GetServer(conn.GetEndPoints()[0]);
        Assert.SkipUnless(api.Version >= NodeIdVersion, $"node ids need {NodeIdVersion}, server is {api.Version}");

        var slots = await api.ClusterSlotsAsync();
        var nodes = await api.ClusterNodesAsync();
        Assert.NotNull(slots);
        Assert.NotNull(nodes);
        // node-id is the identity that does not depend on rendering, so the two views must agree on it -
        // this is the premise that reconciliation keyed on the id relies on
        var fromSlots = slots.Assignments.Select(x => x.Primary.NodeId!).Distinct().OrderBy(x => x).ToArray();
        var fromNodes = nodes.Nodes.Where(x => !x.IsReplica && x.Slots.Count > 0)
            .Select(x => x.NodeId).Distinct().OrderBy(x => x).ToArray();

        Log($"SLOTS: {string.Join(",", fromSlots)}");
        Log($"NODES: {string.Join(",", fromNodes)}");
        fromSlots.Should().Equal(fromNodes);
    }
}
