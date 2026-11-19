namespace Overmind.Core;

// Id is populated only where the marker text itself carried the guid (project:<guid>).
// Every other form stays an unresolved key — the parser never synthesises an identity.
public sealed record WikiLinkTarget(
    EntryLinkKind Kind,
    WikiLinkKeyForm Form,
    string Key,
    Guid? Id = null);
