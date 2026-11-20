namespace Overmind.Infrastructure.Agent;

public sealed record AgentCaller(Guid? ConversationProjectId)
{
    public static readonly AgentCaller Global = new((Guid?)null);

    public string ScopeValue =>
        ConversationProjectId is { } project
            ? project.ToString("D")
            : AgentCallerHeaders.GlobalScopeValue;
}
