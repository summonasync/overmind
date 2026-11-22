using Overmind.Core;

namespace Overmind.Infrastructure.Entries;

// One row the canvas wants stored. Pinned=false keeps the row (the owner's last spot survives an
// unpin in the table) but takes the node out of the read projection, which returns pinned rows only.
public sealed record GraphNodePositionWrite(EntryLinkKind Kind, Guid Id, double X, double Y, bool Pinned);
