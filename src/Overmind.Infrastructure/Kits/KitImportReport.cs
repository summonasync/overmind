namespace Overmind.Infrastructure.Kits;

public sealed class KitImportReport
{
    public required string ScannedPath { get; init; }

    public List<KitImportKitResult> Kits { get; } = [];
}
