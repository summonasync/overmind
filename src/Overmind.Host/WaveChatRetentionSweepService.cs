using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Overmind.Infrastructure.Wave;

namespace Overmind.Host;

public sealed class WaveChatRetentionSweepService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<WaveChatRetentionSweepService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retentionDays = WaveChatRetentionSweeper.NormalizeDays(
            configuration.GetValue<int?>("Wave:ChatRetentionDays"));

        using var timer = new PeriodicTimer(Interval);
        try
        {
            do
            {
                try
                {
                    await SweepAsync(retentionDays, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Wave chat retention sweep failed");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task SweepAsync(int retentionDays, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var purged = await scope.ServiceProvider.GetRequiredService<WaveChatRetentionSweeper>()
            .SweepAsync(retentionDays, ct);
        if (purged > 0)
        {
            logger.LogInformation(
                "Wave chat retention purged {PurgedCount} messages older than {RetentionDays} days",
                purged, retentionDays);
        }
    }
}
