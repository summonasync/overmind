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

        logger.LogInformation("Telegram polling transport started");
        return runner.RunAsync(stoppingToken);
    }
}
