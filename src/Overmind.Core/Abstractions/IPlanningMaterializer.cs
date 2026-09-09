namespace Overmind.Core;

public interface IPlanningMaterializer
{
    Task<PlanningMaterializationReport> SyncAsync(Guid projectId, bool force = false, CancellationToken ct = default);

    Task<IReadOnlyList<PlanningDriftItem>> DetectDriftAsync(Guid projectId, CancellationToken ct = default);

    Task<PlanningMaterializationReport> ClearAsync(Guid projectId, Guid? featureId = null, CancellationToken ct = default);
}
