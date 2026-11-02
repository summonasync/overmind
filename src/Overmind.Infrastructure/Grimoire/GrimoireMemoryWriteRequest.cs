namespace Overmind.Infrastructure.Grimoire;

public sealed record GrimoireMemoryWriteRequest(
    string Content,
    string Scope,
    string Kind,
    IReadOnlyList<string> Tags,
    double Importance,
    Guid? Supersedes);
