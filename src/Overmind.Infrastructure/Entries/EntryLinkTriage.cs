using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Entries;

// The triage definition, written once so the stream filter and task 7's inbox chip cannot drift: a note is
// linked when at least one counting edge touches it in either direction — outgoing or incoming, derived or
// manual. A note with none is unlinked, which is the pile the inbox shows first.
//
// Counterparty hygiene is the backlinks rail's, restated here over both directions: an edge whose source is
// lifted, superseded or tombstone-typed links nothing, and neither does an edge whose target row is gone or
// buried. Origin is deliberately not a filter — the manual column exists for the triage card and counts.
public sealed class EntryLinkTriage
{
    private readonly OvermindDbContext _db;

    public EntryLinkTriage(OvermindDbContext db)
    {
        _db = db;
    }

    public IQueryable<ContextEntry> WhereLinked(IQueryable<ContextEntry> notes) =>
        notes.Where(entry =>
            CountingEdges.Any(link => link.FromEntryId == entry.Id)
            || CountingEdges.Any(link => link.ToKind == EntryLinkKind.Entry && link.ToId == entry.Id));

    public IQueryable<ContextEntry> WhereUnlinked(IQueryable<ContextEntry> notes) =>
        notes.Where(entry =>
            !CountingEdges.Any(link => link.FromEntryId == entry.Id)
            && !CountingEdges.Any(link => link.ToKind == EntryLinkKind.Entry && link.ToId == entry.Id));

    public IQueryable<ContextEntry> WhereLinkedToProject(IQueryable<ContextEntry> notes, Guid projectId) =>
        notes.Where(entry =>
            CountingEdges.Any(link =>
                link.FromEntryId == entry.Id
                && link.ToKind == EntryLinkKind.Project
                && link.ToId == projectId));

    private IQueryable<EntryLink> CountingEdges =>
        from link in _db.EntryLinks
        join source in _db.ContextEntries on link.FromEntryId equals source.Id
        where source.State == ContextEntry.ActiveState
              && source.Type != EntryType.Tombstone
              && (link.ToKind == EntryLinkKind.Entry
                      && _db.ContextEntries.Any(target =>
                          target.Id == link.ToId
                          && target.State == ContextEntry.ActiveState
                          && target.Type != EntryType.Tombstone)
                  || link.ToKind == EntryLinkKind.Project
                      && _db.Projects.Any(project => project.Id == link.ToId)
                  || link.ToKind == EntryLinkKind.Feature
                      && _db.PlanningFeatures.Any(feature => feature.Id == link.ToId))
        select link;
}
