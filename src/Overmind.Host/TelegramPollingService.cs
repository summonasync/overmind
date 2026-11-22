using Microsoft.Extensions.Hosting;
using Overmind.Infrastructure.Telegram;

namespace Overmind.Host;

public sealed class TelegramPollingService(
    TelegramPollingRunner runner,
    IHostEnvironment environment,
    ILogger<TelegramPollingService> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing"))
        {
            return Task.CompletedTask;
        }

        // Registration, not a connection: the first IsRunnable check lives one layer down, so a
        // disabled transport must not claim a bot connection in the log (4bae834f follow-up).
        logger.LogInformation("Telegram polling transport registered — polling starts only if enabled with a bot token");
        return runner.RunAsync(stoppingToken);
    }
}
