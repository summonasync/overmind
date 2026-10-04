using Microsoft.Extensions.Hosting;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host;

public sealed class PlanningMaterializationHostedService(PlanningMaterializationWorker worker)
    : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        worker.ExecuteAsync(stoppingToken);
}
