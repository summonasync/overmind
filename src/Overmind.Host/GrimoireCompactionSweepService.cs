using System.Globalization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Grimoire;

namespace Overmind.Host;

public sealed class GrimoireCompactionSweepService(
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    ILogger<GrimoireCompactionSweepService> logger) : BackgroundService
{
    internal static readonly TimeSpan TickInterval = TimeSpan.FromHours(1);

    private DateTimeOffset? _lastRunAt;

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
                    await SweepIfDueAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Grimoire compaction sweep failed");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
        }
    }

    internal static bool IsDue(DateTimeOffset? lastRunAt, int compactionHours, DateTimeOffset now) =>
        compactionHours > 0 && (lastRunAt is null || now - lastRunAt >= TimeSpan.FromHours(compactionHours));

    internal static int NormalizeHours(string? raw, int fallback) =>
        int.TryParse(raw, CultureInfo.InvariantCulture, out var hours) && hours >= 0 ? hours : fallback;

    private async Task SweepIfDueAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var settings = scope.ServiceProvider.GetRequiredService<AppSettingsService>();
        var hours = NormalizeHours(
            await settings.GetAsync(SettingKeys.AgentGrimoireCompactionHours, ct),
            SettingDefaults.AgentGrimoireCompactionHours);

        var now = DateTimeOffset.UtcNow;
        if (!IsDue(_lastRunAt, hours, now))
        {
            return;
        }

        var result = await scope.ServiceProvider.GetRequiredService<GrimoireCompactionRunner>().RunAsync(ct);
        switch (result.Outcome)
        {
            case GrimoireCompactionOutcome.Completed:
                _lastRunAt = now;
                logger.LogInformation(
                    "Grimoire compaction pass distilled {FactsWritten} facts and forgot {EntriesForgotten} entries " +
                    "from {GroupsSeen} candidate groups ({GroupsSkipped} groups skipped)",
                    result.FactsWritten, result.EntriesForgotten, result.GroupsSeen, result.GroupsSkipped);
                break;
            case GrimoireCompactionOutcome.ProviderMissing:
            case GrimoireCompactionOutcome.CandidateSourceFailed:
                logger.LogWarning(
                    "Grimoire compaction pass skipped: {Reason}", result.Reason);
                break;
            default:
                logger.LogInformation(
                    "Grimoire compaction pass not run: {Reason}", result.Reason);
                break;
        }
    }
}
