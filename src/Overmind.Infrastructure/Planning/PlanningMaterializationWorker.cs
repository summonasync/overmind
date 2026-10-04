using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed class PlanningMaterializationWorker(
    PlanningMaterializationQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<PlanningMaterializationWorker> logger)
{
    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var projectId in queue.Reader.ReadAllAsync(stoppingToken))
            {
                await RunPassAsync(projectId, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
        }

        while (queue.Reader.TryRead(out var pending))
        {
            await RunPassAsync(pending, CancellationToken.None);
        }
    }

    private async Task RunPassAsync(Guid projectId, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var materializer = scope.ServiceProvider.GetRequiredService<IPlanningMaterializer>();
        await PlanningServiceGuard.SyncMaterializationAsync(materializer, logger, projectId, ct);
    }
}
