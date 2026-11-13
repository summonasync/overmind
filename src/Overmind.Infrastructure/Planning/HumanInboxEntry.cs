using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record HumanInboxEntry(
    string Slug,
    string Title,
    string Phase,
    int QueuePosition,
    string? ClaimedByAlias,
    AgentSessionLiveness? ClaimState,
    DateTime PendingSince);
