using Overmind.Core;

namespace Overmind.Infrastructure.VectorSearch;

public sealed class VectorSearchHealthCache
{
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromSeconds(30);

    internal TimeSpan Ttl { get; set; } = DefaultTtl;

    internal Func<DateTimeOffset> UtcNow { get; set; } = static () => DateTimeOffset.UtcNow;

    private volatile Entry? _entry;

    public VectorSearchHealthReport? Read()
    {
        var entry = _entry;
        return entry is not null && UtcNow() - entry.CachedAtUtc < Ttl ? entry.Report : null;
    }

    public void Store(VectorSearchHealthReport report) => _entry = new Entry(UtcNow(), report);

    private sealed record Entry(DateTimeOffset CachedAtUtc, VectorSearchHealthReport Report);
}
