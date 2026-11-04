using Microsoft.Extensions.Hosting;
using Overmind.Infrastructure.Agent;

namespace Overmind.Host;

public sealed class AgentLoopHostedService(
    IHostApplicationLifetime lifetime,
    IHostEnvironment environment,
    IServiceScopeFactory scopeFactory,
    ILogger<AgentLoopHostedService> logger) : BackgroundService
{
    private readonly TaskCompletionSource _started =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        lifetime.ApplicationStarted.Register(() => _started.TrySetResult());
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing"))
        {
            return;
        }

        try
        {
            await _started.Task.WaitAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lifetime.ApplicationStopping.Register(() => _ = EndSessionQuietlyAsync());

        try
        {
            using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<EmbeddedAgentSessionRegistrar>()
                .EnsureRegisteredAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Embedded agent session registration failed at startup; it retries on the next conversation turn");
        }
    }

    private async Task EndSessionQuietlyAsync()
    {
        try
        {
            using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<EmbeddedAgentSessionRegistrar>()
                .EndAsync(CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<NotificationValidatorSessionRegistrar>()
                .EndAsync(CancellationToken.None);
        }
        catch (Exception)
        {
        }
    }
}
