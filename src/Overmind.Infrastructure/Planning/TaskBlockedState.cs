namespace Overmind.Infrastructure.Planning;

public sealed record TaskBlockedState(int Order, bool Blocked, IReadOnlyList<int> BlockedBy);
