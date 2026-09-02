using System;
using System.Text;
using CodeBrix.TestMocks.Mocking;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[Collection(nameof(SubstituteDependentCollection))]
public sealed class KeyPrefixedVectorSetTests
{
    private readonly Mock<IDatabase> mock;
    private readonly IDatabase prefixed;

    public KeyPrefixedVectorSetTests()
    {
        mock = new Mock<IDatabase>();
        prefixed = new KeyspaceIsolation.KeyPrefixedDatabase(mock.Object, Encoding.UTF8.GetBytes("prefix:"));
    }

    [Fact]
    public void vector_set_add_fp32()
    {
        if (BitConverter.IsLittleEndian)
        {
            VectorSetAddMessage.CanUseFp32.Should().BeTrue();
        }
        else
        {
            VectorSetAddMessage.CanUseFp32.Should().BeFalse();
        }
    }

    [Fact]
    public void vector_set_add_basic_call()
    {
        var vector = new[] { 1.0f, 2.0f, 3.0f }.AsMemory();

        var request = VectorSetAddRequest.Member("element1", vector);
        prefixed.VectorSetAdd("vectorset", request);

        mock.Verify(x => x.VectorSetAdd(
            "prefix:vectorset",
            request), Times.AtLeastOnce());
    }

    [Fact]
    public void vector_set_add_with_all_parameters()
    {
        var vector = new[] { 1.0f, 2.0f, 3.0f }.AsMemory();
        var attributes = """{"category":"test"}""";

        var request = VectorSetAddRequest.Member(
            "element1",
            vector,
            attributes);
        request.ReducedDimensions = 64;
        request.Quantization = VectorSetQuantization.Binary;
        request.BuildExplorationFactor = 300;
        request.MaxConnections = 32;
        request.UseCheckAndSet = true;
        prefixed.VectorSetAdd(
            "vectorset",
            request,
            flags: CommandFlags.FireAndForget);

        mock.Verify(x => x.VectorSetAdd(
            "prefix:vectorset",
            request,
            CommandFlags.FireAndForget), Times.AtLeastOnce());
    }

    [Fact]
    public void vector_set_length()
    {
        prefixed.VectorSetLength("vectorset");
        mock.Verify(x => x.VectorSetLength("prefix:vectorset"), Times.AtLeastOnce());
    }

    [Fact]
    public void vector_set_dimension()
    {
        prefixed.VectorSetDimension("vectorset");
        mock.Verify(x => x.VectorSetDimension("prefix:vectorset"), Times.AtLeastOnce());
    }

    [Fact]
    public void vector_set_get_approximate_vector()
    {
        prefixed.VectorSetGetApproximateVector("vectorset", "member1");
        mock.Verify(x => x.VectorSetGetApproximateVector("prefix:vectorset", "member1"), Times.AtLeastOnce());
    }

    [Fact]
    public void vector_set_get_attributes_json()
    {
        prefixed.VectorSetGetAttributesJson("vectorset", "member1");
        mock.Verify(x => x.VectorSetGetAttributesJson("prefix:vectorset", "member1"), Times.AtLeastOnce());
    }

    [Fact]
    public void vector_set_info()
    {
        prefixed.VectorSetInfo("vectorset");
        mock.Verify(x => x.VectorSetInfo("prefix:vectorset"), Times.AtLeastOnce());
    }

    [Fact]
    public void vector_set_contains()
    {
        prefixed.VectorSetContains("vectorset", "member1");
        mock.Verify(x => x.VectorSetContains("prefix:vectorset", "member1"), Times.AtLeastOnce());
    }

    [Fact]
    public void vector_set_get_links()
    {
        prefixed.VectorSetGetLinks("vectorset", "member1");
        mock.Verify(x => x.VectorSetGetLinks("prefix:vectorset", "member1"), Times.AtLeastOnce());
    }

    [Fact]
    public void vector_set_get_links_with_scores()
    {
        prefixed.VectorSetGetLinksWithScores("vectorset", "member1");
        mock.Verify(x => x.VectorSetGetLinksWithScores("prefix:vectorset", "member1"), Times.AtLeastOnce());
    }

    [Fact]
    public void vector_set_random_member()
    {
        prefixed.VectorSetRandomMember("vectorset");
        mock.Verify(x => x.VectorSetRandomMember("prefix:vectorset"), Times.AtLeastOnce());
    }

    [Fact]
    public void vector_set_random_members()
    {
        prefixed.VectorSetRandomMembers("vectorset", 5);
        mock.Verify(x => x.VectorSetRandomMembers("prefix:vectorset", 5), Times.AtLeastOnce());
    }

    [Fact]
    public void vector_set_remove()
    {
        prefixed.VectorSetRemove("vectorset", "member1");
        mock.Verify(x => x.VectorSetRemove("prefix:vectorset", "member1"), Times.AtLeastOnce());
    }

    [Fact]
    public void vector_set_set_attributes_json()
    {
        var attributes = """{"category":"test"}""";

        prefixed.VectorSetSetAttributesJson("vectorset", "member1", attributes);
        mock.Verify(x => x.VectorSetSetAttributesJson("prefix:vectorset", "member1", attributes), Times.AtLeastOnce());
    }

    [Fact]
    public void vector_set_similarity_search_by_vector()
    {
        var vector = new[] { 1.0f, 2.0f, 3.0f }.AsMemory();

        var query = VectorSetSimilaritySearchRequest.ByVector(vector);
        prefixed.VectorSetSimilaritySearch(
            "vectorset",
            query);
        mock.Verify(x => x.VectorSetSimilaritySearch(
            "prefix:vectorset",
            query), Times.AtLeastOnce());
    }

    [Fact]
    public void vector_set_similarity_search_by_member()
    {
        var query = VectorSetSimilaritySearchRequest.ByMember("member1");
        query.Count = 5;
        query.WithScores = true;
        query.WithAttributes = true;
        query.Epsilon = 0.1;
        query.SearchExplorationFactor = 400;
        query.FilterExpression = "category='test'";
        query.MaxFilteringEffort = 1000;
        query.UseExactSearch = true;
        query.DisableThreading = true;
        prefixed.VectorSetSimilaritySearch(
            "vectorset",
            query,
            CommandFlags.FireAndForget);
        mock.Verify(x => x.VectorSetSimilaritySearch(
            "prefix:vectorset",
            query,
            CommandFlags.FireAndForget), Times.AtLeastOnce());
    }

    [Fact]
    public void vector_set_similarity_search_by_vector_default_parameters()
    {
        var vector = new[] { 1.0f, 2.0f }.AsMemory();

        // Test that default parameters work correctly
        var query = VectorSetSimilaritySearchRequest.ByVector(vector);
        prefixed.VectorSetSimilaritySearch("vectorset", query);
        mock.Verify(x => x.VectorSetSimilaritySearch(
            "prefix:vectorset",
            query), Times.AtLeastOnce());
    }
}
