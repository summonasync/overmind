using Microsoft.Extensions.Hosting;
using Overmind.Infrastructure.Notifications;

namespace Overmind.Host;

public sealed class ReplyAutoActHostedService(ReplyAutoActWorker worker) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        worker.ExecuteAsync(stoppingToken);
}
