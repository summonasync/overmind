namespace Overmind.Infrastructure.Agent;

public interface IAgentRunSettingsProvider
{
    Task<AgentRunSettings> GetAsync(CancellationToken ct);
}
