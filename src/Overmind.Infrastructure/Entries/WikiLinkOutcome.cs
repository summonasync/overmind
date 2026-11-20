using Overmind.Core;

namespace Overmind.Infrastructure.Entries;

// ToId null means the marker matched nothing. A ToId equal to the source is a self-link:
// the target exists, the caller drops the edge, and neither reading it as an edge nor as a
// dead end would be right.
public sealed record WikiLinkOutcome(WikiLinkTarget Target, Guid? ToId);
