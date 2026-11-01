using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Overmind.Infrastructure.Wake;

public sealed class WakeDropFanout(IServiceScopeFactory scopes, ILogger<WakeDropFanout> logger)
{
    public async Task WakeAsync(string alias)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var snapshot = await scope.ServiceProvider
                .GetRequiredService<AgentWakeService>()
                .SnapshotAsync(alias, CancellationToken.None);
            if (snapshot is null)
            {
                return;
            }

            await scope.ServiceProvider.GetRequiredService<WakeDropWriter>()
                .WriteAsync(WakeDrop.For(alias, DateTime.UtcNow, snapshot), CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "wake drop write failed for alias {Alias}", alias);
        }
    }
}
