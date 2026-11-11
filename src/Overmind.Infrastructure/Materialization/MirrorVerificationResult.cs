namespace Overmind.Infrastructure.Materialization;

public sealed record MirrorVerificationResult(
    int Total,
    IReadOnlyList<string> Drifted,
    IReadOnlyList<string> Missing)
{
    private const int DetailPathLimit = 10;

    public bool Passed => Drifted.Count == 0 && Missing.Count == 0;

    public string Describe()
    {
        var failed = Drifted.Concat(Missing).Order(StringComparer.Ordinal).ToList();
        var shown = failed.Take(DetailPathLimit).ToArray();
        var hidden = failed.Count - shown.Length;
        var listing = hidden > 0
            ? $"{string.Join(", ", shown)} and {hidden} more."
            : $"{string.Join(", ", shown)}.";

        return
            $"Mirror verification failed: {failed.Count} of {Total} files drifted or missing at the candidate path. Files: {listing}";
    }
}
