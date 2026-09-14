namespace Overmind.Infrastructure.Kits;

public sealed class KitScanIssue
{
    public required string RelativePath { get; init; }

    public required KitScanIssueCode Code { get; init; }

    public required string Message { get; init; }
}
