using System;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[RunPerProtocol]
public sealed class VectorSetIntegrationTests(ITestOutputHelper output) : TestBase(output)
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task vector_set_add_basic_operation(bool useFp32)
    {
        await using var conn = Create(require: RedisFeatures.v8_0_0_M04);
        var db = conn.GetDatabase();
        var key = Me();

        // Clean up any existing data
        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector = new[] { 1.0f, 2.0f, 3.0f, 4.0f };

        var request = VectorSetAddRequest.Member("element1", vector.AsMemory(), null);
        request.UseFp32 = useFp32;
        var result = await db.VectorSetAddAsync(key, request);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task vector_set_add_with_attributes()
    {
        await using var conn = Create(require: RedisFeatures.v8_0_0_M04);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector = new[] { 1.0f, 2.0f, 3.0f, 4.0f };
        var attributes = """{"category":"test","id":123}""";

        var request = VectorSetAddRequest.Member("element1", vector.AsMemory(), attributes);
        var result = await db.VectorSetAddAsync(key, request);

        result.Should().BeTrue();

        // Verify attributes were stored
        var retrievedAttributes = await db.VectorSetGetAttributesJsonAsync(key, "element1");
        retrievedAttributes.Should().Be(attributes);
    }

    [Theory]
    [InlineData(VectorSetQuantization.Int8, false)]
    [InlineData(VectorSetQuantization.None, false)]
    [InlineData(VectorSetQuantization.Binary, false)]
    [InlineData(VectorSetQuantization.Int8, true)]
    [InlineData(VectorSetQuantization.None, true)]
    [InlineData(VectorSetQuantization.Binary, true)]
    public async Task vector_set_add_with_everything(VectorSetQuantization quantization, bool useFp32)
    {
        await using var conn = Create(require: RedisFeatures.v8_0_0_M04);
        var server = conn.GetServer(RedisKey.Null);
        Log($"Server version: {server.Version}");
        var db = conn.GetDatabase();
        var key = Me() + "/" + quantization;

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector = new[] { 1.0f, 2.0f, 3.0f, 4.0f };
        var attributes = """{"category":"test","id":123}""";

        var request = VectorSetAddRequest.Member(
            "element1",
            vector.AsMemory(),
            attributes);
        request.UseFp32 = useFp32;
        request.Quantization = quantization;
        request.ReducedDimensions = 4;
        request.BuildExplorationFactor = 300;
        request.MaxConnections = 32;
        request.UseCheckAndSet = true;
        Log("Storing...");
        var result = await db.VectorSetAddAsync(
            key,
            request);

        result.Should().BeTrue();

        Log("Stored successfully; fetching attributes...");
        // Verify attributes were stored
        var retrievedAttributes = await db.VectorSetGetAttributesJsonAsync(key, "element1");
        retrievedAttributes.Should().Be(attributes);
    }

    [Fact]
    public async Task vector_set_length_empty_set()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v8_0_0_M04);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        //Act
        var length = await db.VectorSetLengthAsync(key);

        //Assert
        length.Should().Be(0);
    }

    [Fact]
    public async Task vector_set_length_with_elements()
    {
        await using var conn = Create(require: RedisFeatures.v8_0_0_M04);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector1 = new[] { 1.0f, 2.0f, 3.0f };
        var vector2 = new[] { 4.0f, 5.0f, 6.0f };

        var request = VectorSetAddRequest.Member("element1", vector1.AsMemory());
        await db.VectorSetAddAsync(key, request);
        request = VectorSetAddRequest.Member("element2", vector2.AsMemory());
        await db.VectorSetAddAsync(key, request);

        var length = await db.VectorSetLengthAsync(key);
        length.Should().Be(2);
    }

    [Fact]
    public async Task vector_set_dimension()
    {
        await using var conn = Create(require: RedisFeatures.v8_0_0_M04);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector = new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f };
        var request = VectorSetAddRequest.Member("element1", vector.AsMemory());
        await db.VectorSetAddAsync(key, request);

        var dimension = await db.VectorSetDimensionAsync(key);
        dimension.Should().Be(5);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task vector_set_contains(bool useFp32)
    {
        await using var conn = Create(require: RedisFeatures.v8_0_0_M04);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector = new[] { 1.0f, 2.0f, 3.0f };
        var request = VectorSetAddRequest.Member("element1", vector.AsMemory());
        request.UseFp32 = useFp32;
        await db.VectorSetAddAsync(key, request);

        var exists = await db.VectorSetContainsAsync(key, "element1");
        var notExists = await db.VectorSetContainsAsync(key, "element2");

        exists.Should().BeTrue();
        notExists.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task vector_set_get_approximate_vector(bool useFp32)
    {
        await using var conn = Create(require: RedisFeatures.v8_0_0_M04);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var originalVector = new[] { 1.0f, 2.0f, 3.0f, 4.0f };
        var request = VectorSetAddRequest.Member("element1", originalVector.AsMemory());
        request.UseFp32 = useFp32;
        await db.VectorSetAddAsync(key, request);

        using var retrievedLease = await db.VectorSetGetApproximateVectorAsync(key, "element1");

        Assert.NotNull(retrievedLease);
        var retrievedVector = retrievedLease.Span;

        retrievedVector.Length.Should().Be(originalVector.Length);
        // Note: Due to quantization, values might not be exactly equal
        for (int i = 0; i < originalVector.Length; i++)
        {
            (Math.Abs(originalVector[i] - retrievedVector[i]) < 0.1f).Should()
                .BeTrue($"Vector component {i} differs too much: expected {originalVector[i]}, got {retrievedVector[i]}");
        }
    }

    [Fact]
    public async Task vector_set_remove()
    {
        await using var conn = Create(require: RedisFeatures.v8_0_0_M04);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector = new[] { 1.0f, 2.0f, 3.0f };
        var request = VectorSetAddRequest.Member("element1", vector.AsMemory());
        await db.VectorSetAddAsync(key, request);

        var removed = await db.VectorSetRemoveAsync(key, "element1");
        removed.Should().BeTrue();

        removed = await db.VectorSetRemoveAsync(key, "element1");
        removed.Should().BeFalse();

        var exists = await db.VectorSetContainsAsync(key, "element1");
        exists.Should().BeFalse();
    }

    [Theory]
    [InlineData(VectorSetQuantization.Int8)]
    [InlineData(VectorSetQuantization.Binary)]
    [InlineData(VectorSetQuantization.None)]
    public async Task vector_set_info(VectorSetQuantization quantization)
    {
        await using var conn = Create(require: RedisFeatures.v8_0_0_M04);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector = new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f };
        var request = VectorSetAddRequest.Member("element1", vector.AsMemory());
        request.Quantization = quantization;
        await db.VectorSetAddAsync(key, request);

        var info = await db.VectorSetInfoAsync(key);

        Assert.NotNull(info);
        var v = info.GetValueOrDefault();
        v.Dimension.Should().Be(5);
        v.Length.Should().Be(1);
        v.Quantization.Should().Be(quantization);
        v.QuantizationRaw.Should().BeNull(); // Should be null for known quant types

        v.VectorSetUid.Should().NotBe(0);
        v.HnswMaxNodeUid.Should().NotBe(0);
    }

    [Fact]
    public async Task vector_set_random_member()
    {
        await using var conn = Create(require: RedisFeatures.v8_0_0_M04);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector1 = new[] { 1.0f, 2.0f, 3.0f };
        var vector2 = new[] { 4.0f, 5.0f, 6.0f };

        var request = VectorSetAddRequest.Member("element1", vector1.AsMemory());
        await db.VectorSetAddAsync(key, request);
        request = VectorSetAddRequest.Member("element2", vector2.AsMemory());
        await db.VectorSetAddAsync(key, request);

        var randomMember = await db.VectorSetRandomMemberAsync(key);
        (randomMember == "element1" || randomMember == "element2").Should().BeTrue();
    }

    [Fact]
    public async Task vector_set_random_members()
    {
        await using var conn = Create(require: RedisFeatures.v8_0_0_M04);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector1 = new[] { 1.0f, 2.0f, 3.0f };
        var vector2 = new[] { 4.0f, 5.0f, 6.0f };
        var vector3 = new[] { 7.0f, 8.0f, 9.0f };

        var request = VectorSetAddRequest.Member("element1", vector1.AsMemory());
        await db.VectorSetAddAsync(key, request);
        request = VectorSetAddRequest.Member("element2", vector2.AsMemory());
        await db.VectorSetAddAsync(key, request);
        request = VectorSetAddRequest.Member("element3", vector3.AsMemory());
        await db.VectorSetAddAsync(key, request);

        var randomMembers = await db.VectorSetRandomMembersAsync(key, 2);

        randomMembers.Length.Should().Be(2);
        randomMembers.Should().AllSatisfy(member =>
            (member == "element1" || member == "element2" || member == "element3").Should().BeTrue());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task vector_set_similarity_search_by_vector(bool withScores, bool withAttributes)
    {
        await using var conn = Create(require: RedisFeatures.v8_0_0_M04);
        var db = conn.GetDatabase();
        var disambiguator = (withScores ? 1 : 0) + (withAttributes ? 2 : 0);
        var key = Me() + disambiguator;

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        // Add some test vectors
        var vector1 = new[] { 1.0f, 0.0f, 0.0f };
        var vector2 = new[] { 0.0f, 1.0f, 0.0f };
        var vector3 = new[] { 0.9f, 0.1f, 0.0f }; // Similar to vector1

        var request =
            VectorSetAddRequest.Member("element1", vector1.AsMemory(), attributesJson: """{"category":"x"}""");
        await db.VectorSetAddAsync(key, request);
        request = VectorSetAddRequest.Member("element2", vector2.AsMemory(), attributesJson: """{"category":"y"}""");
        await db.VectorSetAddAsync(key, request);
        request = VectorSetAddRequest.Member("element3", vector3.AsMemory(), attributesJson: """{"category":"z"}""");
        await db.VectorSetAddAsync(key, request);

        // Search for vectors similar to vector1
        var query = VectorSetSimilaritySearchRequest.ByVector(vector1.AsMemory());
        query.Count = 2;
        query.WithScores = withScores;
        query.WithAttributes = withAttributes;
        using var results = await db.VectorSetSimilaritySearchAsync(key, query);

        Assert.NotNull(results);
        foreach (var result in results.Span)
        {
            Log(result.ToString());
        }

        var resultsArray = results.Span.ToArray();

        (resultsArray.Length <= 2).Should().BeTrue();
        resultsArray.Should().Contain(r => r.Member == "element1");
        var found = resultsArray.First(r => r.Member == "element1");

        if (withAttributes)
        {
            found.AttributesJson.Should().Be("""{"category":"x"}""");
        }
        else
        {
            found.AttributesJson.Should().BeNull();
        }

        double.IsNaN(found.Score).Should().NotBe(withScores);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task vector_set_similarity_search_by_member(bool withScores, bool withAttributes)
    {
        await using var conn = Create(require: RedisFeatures.v8_0_0_M04);
        var db = conn.GetDatabase();
        var disambiguator = (withScores ? 1 : 0) + (withAttributes ? 2 : 0);
        var key = Me() + disambiguator;

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector1 = new[] { 1.0f, 0.0f, 0.0f };
        var vector2 = new[] { 0.0f, 1.0f, 0.0f };

        var request =
            VectorSetAddRequest.Member("element1", vector1.AsMemory(), attributesJson: """{"category":"x"}""");
        await db.VectorSetAddAsync(key, request);
        request = VectorSetAddRequest.Member("element2", vector2.AsMemory(), attributesJson: """{"category":"y"}""");
        await db.VectorSetAddAsync(key, request);

        var query = VectorSetSimilaritySearchRequest.ByMember("element1");
        query.Count = 1;
        query.WithScores = withScores;
        query.WithAttributes = withAttributes;
        using var results = await db.VectorSetSimilaritySearchAsync(key, query);

        Assert.NotNull(results);
        foreach (var result in results.Span)
        {
            Log(result.ToString());
        }

        var resultsArray = results.Span.ToArray();

        resultsArray.Should().ContainSingle();
        resultsArray[0].Member.Should().Be("element1");
        if (withAttributes)
        {
            resultsArray[0].AttributesJson.Should().Be("""{"category":"x"}""");
        }
        else
        {
            resultsArray[0].AttributesJson.Should().BeNull();
        }

        double.IsNaN(resultsArray[0].Score).Should().NotBe(withScores);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task vector_set_similarity_search_with_filter(bool corruptPrefix, bool corruptSuffix)
    {
        await using var conn = Create(require: RedisFeatures.v8_0_0_M04);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        Random rand = new Random();

        float[] vector = new float[50];

        void ScrambleVector()
        {
            var arr = vector;
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = (float)rand.NextDouble();
            }
        }

        string[] regions = new[] { "us-west", "us-east", "eu-west", "eu-east", "ap-south", "ap-north" };
        for (int i = 0; i < 100; i++)
        {
            var region = regions[rand.Next(regions.Length)];
            var json = (corruptPrefix ? "oops" : "")
                       + JsonSerializer.Serialize(new { id = i, region })
                       + (corruptSuffix ? "oops" : "");
            ScrambleVector();
            var request = VectorSetAddRequest.Member($"element{i}", vector.AsMemory(), json);
            await db.VectorSetAddAsync(key, request);
        }

        ScrambleVector();
        var query = VectorSetSimilaritySearchRequest.ByVector(vector);
        query.Count = 100;
        query.WithScores = true;
        query.WithAttributes = true;
        query.FilterExpression = ".id >= 30";
        using var results = await db.VectorSetSimilaritySearchAsync(key, query);

        Assert.NotNull(results);
        foreach (var result in results.Span)
        {
            Log(result.ToString());
        }

        Log($"Total matches: {results.Span.Length}");

        var resultsArray = results.Span.ToArray();
        if (corruptPrefix)
        {
            // server short-circuits failure to be no match; we just want to assert
            // what the observed behavior *is*
            resultsArray.Should().BeEmpty();
        }
        else
        {
            resultsArray.Length.Should().Be(70);
            resultsArray.Should().AllSatisfy(r =>
                (r.Score is > 0.0 and < 1.0 && GetId(r.Member!) >= 30).Should().BeTrue());
        }

        static int GetId(string member)
        {
            if (member.StartsWith("element"))
            {
                return int.Parse(member.Substring(7), NumberStyles.Integer, CultureInfo.InvariantCulture);
            }

            return -1;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData(".id >= 30")]
    public async Task vector_set_similarity_search_test_filter_values(string? filterExpression)
    {
        await using var conn = Create(require: RedisFeatures.v8_0_0_M04);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        Random rand = new Random();

        float[] vector = new float[50];

        void ScrambleVector()
        {
            var arr = vector;
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = (float)rand.NextDouble();
            }
        }

        string[] regions = new[] { "us-west", "us-east", "eu-west", "eu-east", "ap-south", "ap-north" };
        for (int i = 0; i < 100; i++)
        {
            var region = regions[rand.Next(regions.Length)];
            var json = JsonSerializer.Serialize(new { id = i, region });
            ScrambleVector();
            var request = VectorSetAddRequest.Member($"element{i}", vector.AsMemory(), json);
            await db.VectorSetAddAsync(key, request);
        }

        ScrambleVector();
        var query = VectorSetSimilaritySearchRequest.ByVector(vector);
        query.Count = 100;
        query.WithScores = true;
        query.WithAttributes = true;
        query.FilterExpression = filterExpression;

        using var results = await db.VectorSetSimilaritySearchAsync(key, query);

        Assert.NotNull(results);
        foreach (var result in results.Span)
        {
            Log(result.ToString());
        }

        Log($"Total matches: {results.Span.Length}");
        // we're not interested in the specific results; we're just checking that the
        // filter expression was added and parsed without exploding about arg mismatch
    }

    [Fact]
    public async Task vector_set_set_attributes_json()
    {
        await using var conn = Create(require: RedisFeatures.v8_0_0_M04);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector = new[] { 1.0f, 2.0f, 3.0f };
        var request = VectorSetAddRequest.Member("element1", vector.AsMemory());
        await db.VectorSetAddAsync(key, request);

        // Set attributes for existing element
        var attributes = """{"category":"updated","priority":"high","timestamp":"2024-01-01"}""";
        var result = await db.VectorSetSetAttributesJsonAsync(key, "element1", attributes);

        result.Should().BeTrue();

        // Verify attributes were set
        var retrievedAttributes = await db.VectorSetGetAttributesJsonAsync(key, "element1");
        retrievedAttributes.Should().Be(attributes);

        // Try setting attributes for non-existent element
        var failResult = await db.VectorSetSetAttributesJsonAsync(key, "nonexistent", attributes);
        failResult.Should().BeFalse();
    }

    [Fact]
    public async Task vector_set_get_links()
    {
        await using var conn = Create(require: RedisFeatures.v8_0_0_M04);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        // Add some vectors that should be linked
        var vector1 = new[] { 1.0f, 0.0f, 0.0f };
        var vector2 = new[] { 0.9f, 0.1f, 0.0f }; // Similar to vector1
        var vector3 = new[] { 0.0f, 1.0f, 0.0f }; // Different from vector1

        var request = VectorSetAddRequest.Member("element1", vector1.AsMemory());
        await db.VectorSetAddAsync(key, request);
        request = VectorSetAddRequest.Member("element2", vector2.AsMemory());
        await db.VectorSetAddAsync(key, request);
        request = VectorSetAddRequest.Member("element3", vector3.AsMemory());
        await db.VectorSetAddAsync(key, request);

        // Get links for element1 (should include similar vectors)
        using var links = await db.VectorSetGetLinksAsync(key, "element1");

        Assert.NotNull(links);
        foreach (var link in links.Span)
        {
            Log(link.ToString());
        }

        var linksArray = links.Span.ToArray();

        // Should contain the other elements (note there can be transient duplicates, so: contains, not exact)
        linksArray.Should().Contain("element2");
        linksArray.Should().Contain("element3");
    }

    [Fact]
    public async Task vector_set_get_links_with_scores()
    {
        await using var conn = Create(require: RedisFeatures.v8_0_0_M04);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        // Add some vectors with known relationships
        var vector1 = new[] { 1.0f, 0.0f, 0.0f };
        var vector2 = new[] { 0.9f, 0.1f, 0.0f }; // Similar to vector1
        var vector3 = new[] { 0.0f, 1.0f, 0.0f }; // Different from vector1

        var request = VectorSetAddRequest.Member("element1", vector1.AsMemory());
        await db.VectorSetAddAsync(key, request);
        request = VectorSetAddRequest.Member("element2", vector2.AsMemory());
        await db.VectorSetAddAsync(key, request);
        request = VectorSetAddRequest.Member("element3", vector3.AsMemory());
        await db.VectorSetAddAsync(key, request);

        // Get links with scores for element1
        using var linksWithScores = await db.VectorSetGetLinksWithScoresAsync(key, "element1");
        Assert.NotNull(linksWithScores);
        foreach (var link in linksWithScores.Span)
        {
            Log(link.ToString());
        }

        var linksArray = linksWithScores.Span.ToArray();
        linksArray.Should().NotBeEmpty();

        // Verify each link has a valid score
        // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
        linksArray.Should().AllSatisfy(static link =>
        {
            link.Member.IsNull.Should().BeFalse();
            double.IsNaN(link.Score).Should().BeFalse();
            (link.Score >= 0.0).Should().BeTrue(); // Similarity scores should be non-negative
        });

        // Should contain the other elements (note there can be transient duplicates, so: contains, not exact)
        linksArray.Should().Contain(l => l.Member == "element2");
        linksArray.Should().Contain(l => l.Member == "element3");

        (linksArray.First(l => l.Member == "element2").Score > 0.9).Should().BeTrue(); // similar
        (linksArray.First(l => l.Member == "element3").Score < 0.8).Should().BeTrue(); // less-so
    }

    [Fact]
    public async Task vector_set_range_basic_operation()
    {
        await using var conn = Create(require: RedisFeatures.v8_4_0_rc1);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        // Add members with lexicographically ordered names
        var vector = new[] { 1.0f, 2.0f, 3.0f };
        var members = new[] { "alpha", "beta", "delta", "gamma" }; // note: delta before gamma because lexicographical

        foreach (var member in members)
        {
            var request = VectorSetAddRequest.Member(member, vector.AsMemory());
            await db.VectorSetAddAsync(key, request);
        }

        // Get all members - should be in lexicographical order
        using var result = await db.VectorSetRangeAsync(key);

        Assert.NotNull(result);
        result.Length.Should().Be(4);
        // Lexicographical order: alpha, beta, delta, gamma
        result.Span.ToArray().Select(r => (string?)r).ToArray().Should().Equal(new[] { "alpha", "beta", "delta", "gamma" });
    }

    [Fact]
    public async Task vector_set_range_with_start_and_end()
    {
        await using var conn = Create(require: RedisFeatures.v8_4_0_rc1);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector = new[] { 1.0f, 2.0f, 3.0f };
        var members = new[] { "apple", "banana", "cherry", "date", "elderberry" };

        foreach (var member in members)
        {
            var request = VectorSetAddRequest.Member(member, vector.AsMemory());
            await db.VectorSetAddAsync(key, request);
        }

        // Get range from "banana" to "date" (inclusive)
        using var result = await db.VectorSetRangeAsync(key, start: "banana", end: "date");

        Assert.NotNull(result);
        result.Length.Should().Be(3);
        result.Span.ToArray().Select(r => (string?)r).ToArray().Should().Equal(new[] { "banana", "cherry", "date" });
    }

    [Fact]
    public async Task vector_set_range_with_count()
    {
        await using var conn = Create(require: RedisFeatures.v8_4_0_rc1);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector = new[] { 1.0f, 2.0f, 3.0f };

        // Add 10 members
        for (int i = 0; i < 10; i++)
        {
            var request = VectorSetAddRequest.Member($"member{i}", vector.AsMemory());
            await db.VectorSetAddAsync(key, request);
        }

        // Get only 5 members
        using var result = await db.VectorSetRangeAsync(key, count: 5);

        Assert.NotNull(result);
        result.Length.Should().Be(5);
    }

    [Fact]
    public async Task vector_set_range_with_exclude_start()
    {
        await using var conn = Create(require: RedisFeatures.v8_4_0_rc1);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector = new[] { 1.0f, 2.0f, 3.0f };
        var members = new[] { "a", "b", "c", "d" };

        foreach (var member in members)
        {
            var request = VectorSetAddRequest.Member(member, vector.AsMemory());
            await db.VectorSetAddAsync(key, request);
        }

        // Get range excluding start
        using var result = await db.VectorSetRangeAsync(key, start: "a", end: "d", exclude: Exclude.Start);

        Assert.NotNull(result);
        result.Length.Should().Be(3);
        result.Span.ToArray().Select(r => (string?)r).ToArray().Should().Equal(new[] { "b", "c", "d" });
    }

    [Fact]
    public async Task vector_set_range_with_exclude_end()
    {
        await using var conn = Create(require: RedisFeatures.v8_4_0_rc1);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector = new[] { 1.0f, 2.0f, 3.0f };
        var members = new[] { "a", "b", "c", "d" };

        foreach (var member in members)
        {
            var request = VectorSetAddRequest.Member(member, vector.AsMemory());
            await db.VectorSetAddAsync(key, request);
        }

        // Get range excluding end
        using var result = await db.VectorSetRangeAsync(key, start: "a", end: "d", exclude: Exclude.Stop);

        Assert.NotNull(result);
        result.Length.Should().Be(3);
        result.Span.ToArray().Select(r => (string?)r).ToArray().Should().Equal(new[] { "a", "b", "c" });
    }

    [Fact]
    public async Task vector_set_range_with_exclude_both()
    {
        await using var conn = Create(require: RedisFeatures.v8_4_0_rc1);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector = new[] { 1.0f, 2.0f, 3.0f };
        var members = new[] { "a", "b", "c", "d", "e" };

        foreach (var member in members)
        {
            var request = VectorSetAddRequest.Member(member, vector.AsMemory());
            await db.VectorSetAddAsync(key, request);
        }

        // Get range excluding both boundaries
        using var result = await db.VectorSetRangeAsync(key, start: "a", end: "e", exclude: Exclude.Both);

        Assert.NotNull(result);
        result.Length.Should().Be(3);
        result.Span.ToArray().Select(r => (string?)r).ToArray().Should().Equal(new[] { "b", "c", "d" });
    }

    [Fact]
    public async Task vector_set_range_empty_set()
    {
        await using var conn = Create(require: RedisFeatures.v8_4_0_rc1);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        // Don't add any members
        using var result = await db.VectorSetRangeAsync(key);

        Assert.NotNull(result);
        result.Span.ToArray().Should().BeEmpty();
    }

    [Fact]
    public async Task vector_set_range_no_matches()
    {
        await using var conn = Create(require: RedisFeatures.v8_4_0_rc1);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector = new[] { 1.0f, 2.0f, 3.0f };
        var members = new[] { "a", "b", "c" };

        foreach (var member in members)
        {
            var request = VectorSetAddRequest.Member(member, vector.AsMemory());
            await db.VectorSetAddAsync(key, request);
        }

        // Query range with no matching members
        using var result = await db.VectorSetRangeAsync(key, start: "x", end: "z");

        Assert.NotNull(result);
        result.Span.ToArray().Should().BeEmpty();
    }

    [Fact]
    public async Task vector_set_range_open_start()
    {
        await using var conn = Create(require: RedisFeatures.v8_4_0_rc1);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector = new[] { 1.0f, 2.0f, 3.0f };
        var members = new[] { "alpha", "beta", "gamma" };

        foreach (var member in members)
        {
            var request = VectorSetAddRequest.Member(member, vector.AsMemory());
            await db.VectorSetAddAsync(key, request);
        }

        // Get from beginning to "beta"
        using var result = await db.VectorSetRangeAsync(key, end: "beta");

        Assert.NotNull(result);
        result.Length.Should().Be(2);
        result.Span.ToArray().Select(r => (string?)r).ToArray().Should().Equal(new[] { "alpha", "beta" });
    }

    [Fact]
    public async Task vector_set_range_open_end()
    {
        await using var conn = Create(require: RedisFeatures.v8_4_0_rc1);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector = new[] { 1.0f, 2.0f, 3.0f };
        var members = new[] { "alpha", "beta", "gamma" };

        foreach (var member in members)
        {
            var request = VectorSetAddRequest.Member(member, vector.AsMemory());
            await db.VectorSetAddAsync(key, request);
        }

        // Get from "beta" to end
        using var result = await db.VectorSetRangeAsync(key, start: "beta");

        Assert.NotNull(result);
        result.Length.Should().Be(2);
        result.Span.ToArray().Select(r => (string?)r).ToArray().Should().Equal(new[] { "beta", "gamma" });
    }

    [Fact]
    public async Task vector_set_range_sync_vs_async()
    {
        await using var conn = Create(require: RedisFeatures.v8_4_0_rc1);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector = new[] { 1.0f, 2.0f, 3.0f };

        // Add 20 members
        for (int i = 0; i < 20; i++)
        {
            var request = VectorSetAddRequest.Member($"m{i:D2}", vector.AsMemory());
            await db.VectorSetAddAsync(key, request);
        }

        // Call both sync and async
        using var syncResult = db.VectorSetRange(key, start: "m05", end: "m15");
        using var asyncResult = await db.VectorSetRangeAsync(key, start: "m05", end: "m15");

        Assert.NotNull(syncResult);
        Assert.NotNull(asyncResult);
        asyncResult.Length.Should().Be(syncResult.Length);
        asyncResult.Span.ToArray().Select(r => (string?)r).Should().Equal(syncResult.Span.ToArray().Select(r => (string?)r));
    }

    [Fact]
    public async Task vector_set_range_with_numeric_lex_order()
    {
        await using var conn = Create(require: RedisFeatures.v8_4_0_rc1);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector = new[] { 1.0f, 2.0f, 3.0f };
        var members = new[] { "1", "10", "2", "20", "3" };

        foreach (var member in members)
        {
            var request = VectorSetAddRequest.Member(member, vector.AsMemory());
            await db.VectorSetAddAsync(key, request);
        }

        // Get all - should be in lexicographical order, not numeric
        using var result = await db.VectorSetRangeAsync(key);

        Assert.NotNull(result);
        result.Length.Should().Be(5);
        // Lexicographical order: "1", "10", "2", "20", "3"
        result.Span.ToArray().Select(r => (string?)r).ToArray().Should().Equal(new[] { "1", "10", "2", "20", "3" });
    }

    [Fact]
    public async Task vector_set_range_enumerate_basic_iteration()
    {
        await using var conn = Create(require: RedisFeatures.v8_4_0_rc1);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector = new[] { 1.0f, 2.0f, 3.0f };

        // Add 50 members
        for (int i = 0; i < 50; i++)
        {
            var request = VectorSetAddRequest.Member($"member{i:D3}", vector.AsMemory());
            await db.VectorSetAddAsync(key, request);
        }

        // Enumerate with batch size of 10
        var allMembers = new System.Collections.Generic.List<RedisValue>();
        foreach (var member in db.VectorSetRangeEnumerate(key, count: 10))
        {
            allMembers.Add(member);
        }

        allMembers.Count.Should().Be(50);

        // Verify lexicographical order
        var sorted = allMembers.OrderBy(m => (string?)m, StringComparer.Ordinal).ToList();
        allMembers.Should().Equal(sorted);
    }

    [Fact]
    public async Task vector_set_range_enumerate_with_range()
    {
        await using var conn = Create(require: RedisFeatures.v8_4_0_rc1);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector = new[] { 1.0f, 2.0f, 3.0f };

        // Add members "a" through "z"
        for (char c = 'a'; c <= 'z'; c++)
        {
            var request = VectorSetAddRequest.Member(c.ToString(), vector.AsMemory());
            await db.VectorSetAddAsync(key, request);
        }

        // Enumerate from "f" to "p" with batch size 5
        var allMembers = new System.Collections.Generic.List<RedisValue>();
        foreach (var member in db.VectorSetRangeEnumerate(key, start: "f", end: "p", count: 5))
        {
            allMembers.Add(member);
        }

        // Should get "f" through "p" inclusive (11 members)
        allMembers.Count.Should().Be(11);
        ((string?)allMembers.First()).Should().Be("f");
        ((string?)allMembers.Last()).Should().Be("p");
    }

    [Fact]
    public async Task vector_set_range_enumerate_early_break()
    {
        await using var conn = Create(require: RedisFeatures.v8_4_0_rc1);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector = new[] { 1.0f, 2.0f, 3.0f };

        // Add 100 members
        for (int i = 0; i < 100; i++)
        {
            var request = VectorSetAddRequest.Member($"member{i:D3}", vector.AsMemory());
            await db.VectorSetAddAsync(key, request);
        }

        // Take only first 25 members
        var limitedMembers = db.VectorSetRangeEnumerate(key, count: 10).Take(25).ToList();

        limitedMembers.Count.Should().Be(25);
    }

    [Fact]
    public async Task vector_set_range_enumerate_empty_batches()
    {
        await using var conn = Create(require: RedisFeatures.v8_4_0_rc1);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        // Don't add any members
        var allMembers = new System.Collections.Generic.List<RedisValue>();
        foreach (var member in db.VectorSetRangeEnumerate(key))
        {
            allMembers.Add(member);
        }

        allMembers.Should().BeEmpty();
    }

    [Fact]
    public async Task vector_set_range_enumerate_async_basic_iteration()
    {
        await using var conn = Create(require: RedisFeatures.v8_4_0_rc1);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector = new[] { 1.0f, 2.0f, 3.0f };

        // Add 50 members
        for (int i = 0; i < 50; i++)
        {
            var request = VectorSetAddRequest.Member($"member{i:D3}", vector.AsMemory());
            await db.VectorSetAddAsync(key, request);
        }

        // Enumerate with batch size of 10
        var allMembers = new System.Collections.Generic.List<RedisValue>();
        await foreach (var member in db.VectorSetRangeEnumerateAsync(key, count: 10))
        {
            allMembers.Add(member);
        }

        allMembers.Count.Should().Be(50);

        // Verify lexicographical order
        var sorted = allMembers.OrderBy(m => (string?)m, StringComparer.Ordinal).ToList();
        allMembers.Should().Equal(sorted);
    }

    [Fact]
    public async Task vector_set_range_enumerate_async_with_cancellation()
    {
        await using var conn = Create(require: RedisFeatures.v8_4_0_rc1);
        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var vector = new[] { 1.0f, 2.0f, 3.0f };

        // Add 100 members
        for (int i = 0; i < 100; i++)
        {
            var request = VectorSetAddRequest.Member($"member{i:D3}", vector.AsMemory());
            await db.VectorSetAddAsync(key, request);
        }

        using var cts = new CancellationTokenSource();
        var allMembers = new System.Collections.Generic.List<RedisValue>();

        // Start enumeration and cancel after collecting some members
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var member in db.VectorSetRangeEnumerateAsync(key, count: 10).WithCancellation(cts.Token))
            {
                allMembers.Add(member);

                // Cancel after we've collected 25 members
                if (allMembers.Count == 25)
                {
                    cts.Cancel();
                }
            }
        });

        // Should have stopped at or shortly after 25 members
        Log($"Expected ~25 members, got {allMembers.Count}");
        (allMembers.Count >= 25 && allMembers.Count <= 35).Should().BeTrue($"Expected ~25 members, got {allMembers.Count}");
    }
}
