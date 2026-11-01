using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Overmind.Core;

namespace Overmind.Infrastructure.Wake;

public sealed class WakeDropFanout(IServiceScopeFactory scopes, ILogger<WakeDropFanout> logger)
{
    public async Task WakeAsync(string alias)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var provider = scope.ServiceProvider;
            var writer = provider.GetRequiredService<WakeDropWriter>();
            var removed = writer.SweepStale();
            if (removed > 0)
            {
                logger.LogInformation(
                    "Wake drop sweep removed {RemovedCount} stale files", removed);
            }

            var snapshot = await provider
                .GetRequiredService<AgentWakeService>()
                .SnapshotAsync(alias, CancellationToken.None);
            if (snapshot is null)
            {
                return;
            }

            await writer.WriteAsync(
                WakeDrop.For(WaveWatermark.NormalizeAlias(alias), DateTime.UtcNow, snapshot),
                CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "wake drop write failed for alias {Alias}", alias);
        }
    }
}
