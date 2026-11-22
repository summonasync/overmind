namespace Overmind.Infrastructure.Entries;

public sealed record TargetLabel(string Label, string? Slug = null, Guid? ProjectId = null);
