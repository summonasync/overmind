using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;

namespace Overmind.Infrastructure.Planning;

public sealed class PlanningMaterializationDispatcher(
    PlanningMaterializationQueue queue,
    AppSettingsService settings,
    IPlanningMaterializer materializer,
    ILogger<PlanningMaterializationDispatcher> logger)
{
    public async Task DispatchAsync(Guid projectId, CancellationToken ct = default)
    {
        if (await IsInlineAsync(ct) || !queue.TryEnqueue(projectId))
        {
            await DispatchInlineAsync(projectId, ct);
        }
    }

    public async Task DispatchInlineAsync(Guid projectId, CancellationToken ct = default) =>
        await PlanningServiceGuard.SyncMaterializationAsync(materializer, logger, projectId, ct);

    private async Task<bool> IsInlineAsync(CancellationToken ct) =>
        string.Equals(
            await settings.GetOrDefaultAsync(
                SettingKeys.PlanningMaterializationSync,
                SettingDefaults.PlanningMaterializationSync, ct),
            "on", StringComparison.OrdinalIgnoreCase);
}
