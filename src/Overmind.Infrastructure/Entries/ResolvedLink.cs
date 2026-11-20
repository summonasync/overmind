using Overmind.Core;

namespace Overmind.Infrastructure.Entries;

public sealed record ResolvedLink(EntryLinkKind Kind, Guid ToId);
