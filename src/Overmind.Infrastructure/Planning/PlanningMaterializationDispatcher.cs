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
        if (await IsInlineAsync(ct))
        {
            await DispatchInlineAsync(projectId, ct);
        }
        else if (!queue.TryEnqueue(projectId))
        {
            // Queue-full fallback: the request token would abort a half-run pass with no
            // 074 trace and no queued retry — the exact silent-staleness this queue exists
            // to prevent. The pass must survive the client walking away.
            await DispatchInlineAsync(projectId, CancellationToken.None);
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
