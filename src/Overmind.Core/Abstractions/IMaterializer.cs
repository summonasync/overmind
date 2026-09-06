namespace Overmind.Core;

public interface IMaterializer
{
    Task<MaterializationReport> MaterializeAsync(Guid projectId, Guid kitId, CancellationToken ct);

    Task<MaterializationReport> DematerializeAsync(Guid projectId, Guid kitId, CancellationToken ct);

    Task<IReadOnlyList<DriftItem>> DetectDriftAsync(Guid projectId, CancellationToken ct);
}
