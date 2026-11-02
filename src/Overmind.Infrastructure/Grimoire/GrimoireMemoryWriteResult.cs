namespace Overmind.Infrastructure.Grimoire;

public sealed record GrimoireMemoryWriteResult(Guid Id, Guid? SupersededId, string Tome);
