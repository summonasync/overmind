using System.Text.Json.Serialization;
using Overmind.Core;

namespace Overmind.Host.Api.Contracts;

[JsonPolymorphic]
[JsonDerivedType(typeof(EntryHitResponse))]
[JsonDerivedType(typeof(FeatureHitResponse))]
[JsonDerivedType(typeof(ArtifactHitResponse))]
public abstract record SearchHitResponse(string Source, string Snippet, double Rank);

public sealed record EntryHitResponse(
    Guid EntryId,
    string Title,
    EntryType Type,
    string Snippet,
    double Rank) : SearchHitResponse("entry", Snippet, Rank);

public sealed record FeatureHitResponse(
    string FeatureSlug,
    string Title,
    string Snippet,
    double Rank) : SearchHitResponse("feature", Snippet, Rank);

public sealed record ArtifactHitResponse(
    Guid ArtifactId,
    string FeatureSlug,
    PlanningArtifactKind Kind,
    int Order,
    string Label,
    string Snippet,
    double Rank) : SearchHitResponse("artifact", Snippet, Rank);

public sealed record SearchResponse(string Query, IReadOnlyList<SearchHitResponse> Hits);
