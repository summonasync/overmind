using Overmind.Core;

namespace Overmind.Infrastructure.Kits;

public sealed class KitImportKitResult
{
    public required string Name { get; init; }

    public Guid? KitId { get; init; }

    public KitImportFailureKind? FailureKind { get; init; }

    public string? Error { get; init; }

    public bool Success => FailureKind is null && Error is null;

    internal static KitImportKitResult Imported(string name, Guid kitId) =>
        new() { Name = name, KitId = kitId };

    internal static KitImportKitResult Failed(string name, KitImportFailureKind? kind, string error) =>
        new() { Name = name, FailureKind = kind, Error = error };
}
