using Microsoft.Extensions.DependencyInjection;

namespace Overmind.Infrastructure.Agent;

public sealed class NotificationValidatorSessionRegistrar(
    AgentLoopbackClient api,
    [FromKeyedServices(NotificationValidatorSessionRegistrar.Alias)] EmbeddedAgentSessionState state,
    IAgentRunSettingsProvider settingsProvider) : EmbeddedAgentSessionRegistrar(api, state, settingsProvider, Alias)
{
    public const string Alias = "notification-validator";
}
