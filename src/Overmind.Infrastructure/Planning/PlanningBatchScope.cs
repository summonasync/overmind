using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed class PlanningBatchScope
{
    private readonly List<AgentNotification> _stagedNotifications = [];

    public bool Active { get; internal set; }

    public IReadOnlyList<AgentNotification> StagedNotifications => _stagedNotifications;

    internal void Stage(AgentNotification notification) => _stagedNotifications.Add(notification);

    internal void ClearStagedNotifications() => _stagedNotifications.Clear();
}
