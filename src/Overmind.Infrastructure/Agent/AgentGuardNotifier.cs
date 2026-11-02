namespace Overmind.Infrastructure.Agent;

public interface IAgentGuardNotifier
{
    Task NotifyGuardAsync(string title, string detail, Guid? projectId, CancellationToken ct);
}
