namespace Overmind.Infrastructure.Agent;

public interface IAgentDelegationGate
{
    Task<bool> IsEnabledAsync(Guid? projectId, CancellationToken ct = default);
}
