namespace Overmind.Core;

public enum GlobalRunKind
{
    Attempt = 1,
    Success = 2,
}

public sealed class GlobalMaterializationRun
{
    private GlobalMaterializationRun()
    {
    }

    public Guid Id { get; private set; }
    public GlobalRunKind Kind { get; private set; }
    public string ReportJson { get; private set; } = string.Empty;
    public DateTime RecordedAt { get; private set; }

    public static GlobalMaterializationRun Record(GlobalRunKind kind, string reportJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportJson);

        return new GlobalMaterializationRun
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            ReportJson = reportJson,
            RecordedAt = DateTime.UtcNow
        };
    }
}
