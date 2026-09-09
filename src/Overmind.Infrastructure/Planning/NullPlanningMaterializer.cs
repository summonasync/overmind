using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed class NullPlanningMaterializer : IPlanningMaterializer
{
    public Task<PlanningMaterializationReport> SyncAsync(Guid projectId, bool force = false, CancellationToken ct = default) =>
        Task.FromResult(new PlanningMaterializationReport(
            projectId,
            PlanningMaterialization.Off,
            [],
            []));

    public Task<IReadOnlyList<PlanningDriftItem>> DetectDriftAsync(Guid projectId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PlanningDriftItem>>([]);

    public Task<PlanningMaterializationReport> ClearAsync(Guid projectId, Guid? featureId = null, CancellationToken ct = default) =>
        Task.FromResult(new PlanningMaterializationReport(
            projectId,
            PlanningMaterialization.Off,
            [],
            []));
}
