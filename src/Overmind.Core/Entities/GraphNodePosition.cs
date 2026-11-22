namespace Overmind.Core;

public sealed class GraphNodePosition
{
    private GraphNodePosition()
    {
    }

    public EntryLinkKind NodeKind { get; private set; }
    public Guid NodeId { get; private set; }
    public double X { get; private set; }
    public double Y { get; private set; }
    public bool Pinned { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static GraphNodePosition Write(
        EntryLinkKind nodeKind, Guid nodeId, double x, double y, bool pinned, DateTime updatedAt)
    {
        if (!Enum.IsDefined(nodeKind) || nodeKind == EntryLinkKind.Task)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nodeKind), nodeKind, "A position belongs to a node the field draws, and a task never is one.");
        }

        if (nodeId == Guid.Empty)
        {
            throw new ArgumentException("A position requires a node id.", nameof(nodeId));
        }

        if (!double.IsFinite(x) || !double.IsFinite(y))
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, "A coordinate must be a finite number.");
        }

        return new GraphNodePosition
        {
            NodeKind = nodeKind,
            NodeId = nodeId,
            X = x,
            Y = y,
            Pinned = pinned,
            UpdatedAt = updatedAt
        };
    }

    public void PinAt(double x, double y, DateTime updatedAt)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y))
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, "A coordinate must be a finite number.");
        }

        X = x;
        Y = y;
        Pinned = true;
        UpdatedAt = updatedAt;
    }

    public void Release(double x, double y, DateTime updatedAt)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y))
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, "A coordinate must be a finite number.");
        }

        X = x;
        Y = y;
        Pinned = false;
        UpdatedAt = updatedAt;
    }
}
