using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Search;

namespace Overmind.Host.Api.Mapping;

public static class SearchHitMapping
{
    public static SearchHitResponse ToResponse(FulltextHit hit) => hit switch
    {
        EntryFulltextHit entry => new EntryHitResponse(
            entry.EntryId, entry.Title, entry.Type, entry.Snippet, entry.Rank),
        FeatureFulltextHit feature => new FeatureHitResponse(
            feature.FeatureSlug, feature.Title, feature.Snippet, feature.Rank),
        ArtifactFulltextHit artifact => new ArtifactHitResponse(
            artifact.ArtifactId, artifact.FeatureSlug, artifact.Kind, artifact.Order,
            artifact.Label, artifact.Snippet, artifact.Rank),
        _ => throw new InvalidOperationException($"Unknown fulltext hit type '{hit.GetType().Name}'.")
    };
}
