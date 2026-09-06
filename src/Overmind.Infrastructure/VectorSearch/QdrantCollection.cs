using System.Text.Json;
using Overmind.Core;
using Qdrant.Client.Grpc;

namespace Overmind.Infrastructure.VectorSearch;

public static class QdrantCollection
{
    public const string Name = "overmind_entries";

    public static VectorParams CreateVectorParams(int dimensions)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dimensions);
        return new VectorParams { Size = (ulong)dimensions, Distance = Distance.Cosine };
    }

    public static bool HasMatchingDimensions(VectorsConfig vectorsConfig, int dimensions)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dimensions);
        return vectorsConfig.Params is { } parameters && parameters.Size == (ulong)dimensions;
    }
}

public static class EntryPayload
{
    public const string EntryId = "entryId";
    public const string ProjectId = "projectId";
    public const string Type = "type";
    public const string Title = "title";
    public const string Content = "content";

    private const int SnippetLength = 200;

    public static PointStruct ToPoint(ContextEntry entry, float[] vector)
    {
        return new PointStruct
        {
            Id = entry.Id,
            Vectors = vector,
            Payload =
            {
                [EntryId] = entry.Id.ToString(),
                [ProjectId] = entry.ProjectId.ToString(),
                [Type] = JsonNamingPolicy.CamelCase.ConvertName(entry.Type.ToString()),
                [Title] = entry.Title,
                [Content] = entry.Content,
            },
        };
    }

    public static EntrySearchHit ToHit(ScoredPoint point)
    {
        var payload = point.Payload;
        var content = payload[Content].StringValue;
        var snippet = content.Length <= SnippetLength ? content : content[..SnippetLength] + "…";

        return new EntrySearchHit(
            Guid.Parse(payload[EntryId].StringValue),
            payload[Title].StringValue,
            Enum.Parse<EntryType>(payload[Type].StringValue, ignoreCase: true),
            snippet,
            point.Score);
    }
}
