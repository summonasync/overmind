using Overmind.Core;

namespace Overmind.Infrastructure.Search;

public abstract record FulltextHit(string Snippet, double Rank);

public sealed record EntryFulltextHit(
    Guid EntryId,
    string Title,
    EntryType Type,
    string Snippet,
    double Rank) : FulltextHit(Snippet, Rank);

public sealed record FeatureFulltextHit(
    string FeatureSlug,
    string Title,
    string Snippet,
    double Rank) : FulltextHit(Snippet, Rank);

public sealed record ArtifactFulltextHit(
    Guid ArtifactId,
    string FeatureSlug,
    PlanningArtifactKind Kind,
    int Order,
    string Label,
    string Snippet,
    double Rank) : FulltextHit(Snippet, Rank);
