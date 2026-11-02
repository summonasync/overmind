namespace Overmind.Infrastructure.Agent;

public readonly record struct AgentToolInvocation(bool DelegationArmed, Guid? InstructionMessageId);
