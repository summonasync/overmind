namespace Overmind.Infrastructure.Grimoire;

public sealed record GrimoireMemoryRecord(
    Guid Id,
    string Content,
    string Kind,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastAccessedAt,
    string? ActorAlias);
