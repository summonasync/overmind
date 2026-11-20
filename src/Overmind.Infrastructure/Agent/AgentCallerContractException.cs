namespace Overmind.Infrastructure.Agent;

public sealed class AgentCallerContractException : Exception
{
    public AgentCallerContractException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
