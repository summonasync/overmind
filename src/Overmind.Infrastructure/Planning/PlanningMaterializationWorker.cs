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
        List<Guid> interrupted = [];
        try
        {
            await foreach (var projectId in queue.DequeueAllAsync(stoppingToken))
            {
                if (await RunPassAsync(projectId, stoppingToken))
                {
                    interrupted.Add(projectId);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }

        foreach (var projectId in interrupted)
        {
            await RunPassAsync(projectId, CancellationToken.None);
        }

        while (queue.TryDequeue(out var pending))
        {
            await RunPassAsync(pending, CancellationToken.None);
        }
    }

    private async Task<bool> RunPassAsync(Guid projectId, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var materializer = scope.ServiceProvider.GetRequiredService<IPlanningMaterializer>();
        return await PlanningServiceGuard.TrySyncMaterializationAsync(materializer, logger, projectId, ct);
    }
}
