using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Overmind.Infrastructure.Notifications;

namespace Overmind.Host;

public sealed class HumanInboxDigestSweepService(
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    BrowserOrigin origin,
    ILogger<HumanInboxDigestSweepService> logger) : BackgroundService
{
    internal static readonly TimeSpan TickInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing"))
        {
            return;
        }

        using var timer = new PeriodicTimer(TickInterval);
        try
        {
            do
            {
                try
                {
                    await SweepOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Human inbox digest sweep failed");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task SweepOnceAsync(CancellationToken ct)
    {
        var bound = origin.BoundOrigin;
        if (bound is null)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<HumanDigestSweeper>()
            .SweepAsync(
                (projectId, slug) => NotificationLinks.Dashboard(bound, projectId, slug, "result")!,
                DateTime.UtcNow,
                ct);

        if (result.DigestsPosted > 0)
        {
            logger.LogInformation(
                "Human inbox digest posted {DigestsPosted} digests across {ProjectsScanned} digest-enabled projects",
                result.DigestsPosted, result.ProjectsScanned);
        }
    }
}
