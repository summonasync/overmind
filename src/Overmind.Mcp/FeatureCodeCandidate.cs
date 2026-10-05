namespace Overmind.Mcp;

public sealed record FeatureCodeCandidate(
    string Kind,
    string Name,
    IReadOnlyList<string> Names,
    string? Path,
    string? Ref,
    string? HeadSha,
    bool Stale);
