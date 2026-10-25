using Google.Protobuf.Collections;
using Overmind.Core;
using Overmind.Infrastructure.Embeddings;
using Overmind.Infrastructure.VectorSearch;
using Qdrant.Client.Grpc;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class QdrantCollectionConfigTests
{
    [Fact]
    public void CreateVectorParams_MatchesOnnxProviderDimensions()
    {
        var provider = new OnnxEmbeddingProvider(new StubPipeline());

        var parameters = QdrantCollection.CreateVectorParams(provider.Dimensions);

        parameters.Size.ShouldBe((ulong)OnnxEmbeddingProvider.OutputDimensions);
        parameters.Distance.ShouldBe(Distance.Cosine);
    }

    [Fact]
    public void CreateVectorParams_RejectsNonPositiveDimensions()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => QdrantCollection.CreateVectorParams(0));
        Should.Throw<ArgumentOutOfRangeException>(() => QdrantCollection.CreateVectorParams(-1));
    }

    [Fact]
    public void HasMatchingDimensions_ComparesAgainstSingleVectorParams()
    {
        var config = new VectorsConfig { Params = new VectorParams { Size = 384, Distance = Distance.Cosine } };

        QdrantCollection.HasMatchingDimensions(config, 384).ShouldBeTrue();
        QdrantCollection.HasMatchingDimensions(config, 1536).ShouldBeFalse();
    }

    [Fact]
    public void HasMatchingDimensions_FalseForNamedVectorConfig()
    {
        var config = new VectorsConfig
        {
            ParamsMap = new VectorParamsMap
            {
                Map = { ["default"] = new VectorParams { Size = 384, Distance = Distance.Cosine } },
            },
        };

        QdrantCollection.HasMatchingDimensions(config, 384).ShouldBeFalse();
    }

    [Fact]
    public void HasMatchingDimensions_RejectsNonPositiveDimensions()
    {
        var config = new VectorsConfig { Params = new VectorParams { Size = 384, Distance = Distance.Cosine } };

        Should.Throw<ArgumentOutOfRangeException>(() => QdrantCollection.HasMatchingDimensions(config, 0));
    }

    [Fact]
    public void EntryPayload_RoundTripsBetweenPointAndHit()
    {
        var entry = ContextEntry.Create(Guid.NewGuid(), EntryType.Note, "Quarterly plan", "Ship the MVP and iterate");
        var vector = Enumerable.Repeat(0.5f, OnnxEmbeddingProvider.OutputDimensions).ToArray();

        var point = EntryPayload.ToPoint(entry, vector);

        Guid.Parse(point.Id.Uuid).ShouldBe(entry.Id);
        point.Payload[EntryPayload.EntryId].StringValue.ShouldBe(entry.Id.ToString());
        point.Payload[EntryPayload.ProjectId].StringValue.ShouldBe(entry.ProjectId.ToString());
        point.Payload[EntryPayload.Type].StringValue.ShouldBe("note");
        point.Payload[EntryPayload.Title].StringValue.ShouldBe("Quarterly plan");

        var hit = EntryPayload.ToHit(ScoredPointWithPayload(entry.Id, 0.87f, point.Payload));

        hit.ShouldNotBeNull();
        hit!.EntryId.ShouldBe(entry.Id);
        hit.Title.ShouldBe("Quarterly plan");
        hit.Type.ShouldBe(EntryType.Note);
        hit.Snippet.ShouldBe("Ship the MVP and iterate");
        hit.Rank.ShouldBe(0.87, tolerance: 0.000001);
    }

    [Fact]
    public void EntryPayload_TruncatesLongContentForSnippet()
    {
        var entry = ContextEntry.Create(Guid.NewGuid(), EntryType.Note, "Long", new string('x', 500));
        var point = EntryPayload.ToPoint(entry, [1f]);

        var hit = EntryPayload.ToHit(ScoredPointWithPayload(entry.Id, 0f, point.Payload));

        hit.ShouldNotBeNull();
        hit!.Snippet.Length.ShouldBeLessThan(210);
        hit.Snippet.ShouldEndWith("…");
    }

    [Fact]
    public void EntryPayload_IdeaPayload_ParsesAndLegacyFeatureRequestDrops()
    {
        var idea = ContextEntry.Create(Guid.NewGuid(), EntryType.Idea, "Vector cache", "Origin: chat, 2026-10-13.");
        var ideaPoint = EntryPayload.ToPoint(idea, [1f]);
        ideaPoint.Payload[EntryPayload.Type].StringValue.ShouldBe("idea");

        var ideaHit = EntryPayload.ToHit(ScoredPointWithPayload(idea.Id, 0.5f, ideaPoint.Payload));

        ideaHit.ShouldNotBeNull();
        ideaHit!.Type.ShouldBe(EntryType.Idea);

        ideaPoint.Payload[EntryPayload.Type].StringValue = "featureRequest";

        EntryPayload.ToHit(ScoredPointWithPayload(idea.Id, 0.5f, ideaPoint.Payload)).ShouldBeNull();
    }

    private static ScoredPoint ScoredPointWithPayload(Guid id, float score, MapField<string, Value> payload)
    {
        var scored = new ScoredPoint { Id = id, Score = score };
        foreach (var (key, value) in payload)
        {
            scored.Payload.Add(key, value);
        }

        return scored;
    }

    private sealed class StubPipeline : IOnnxEmbeddingPipeline
    {
        public int Dimensions => OnnxEmbeddingProvider.OutputDimensions;

        public float[] Embed(string text) => [1f];

        public void Dispose()
        {
        }
    }
}
