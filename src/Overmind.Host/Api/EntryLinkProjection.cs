using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Entries;

namespace Overmind.Host.Api;

internal static class EntryLinkProjection
{
    public static async Task<EntryLinkView> LoadAsync(
        EntryLinkResolver resolver,
        EntryLinkReader links,
        ContextEntry entry,
        CancellationToken ct)
    {
        var outcomes = await resolver.ResolveOutcomesForReadAsync(
            entry.Id, WikiLinkParser.Parse(entry.Content), ct);

        var drafts = new List<(ResolvedLink Link, string Written)>();
        var unresolved = new List<string>();
        var seen = new HashSet<(EntryLinkKind, Guid)>();

        foreach (var outcome in outcomes)
        {
            if (outcome.ToId is not { } targetId)
            {
                unresolved.Add(Verbatim(outcome.Target));
                continue;
            }

            if (targetId == entry.Id || !seen.Add((outcome.Target.Kind, targetId)))
            {
                continue;
            }

            drafts.Add((new ResolvedLink(outcome.Target.Kind, targetId), outcome.Target.Key));
        }

        // Markers first in document order, then the stored manual edges the door writes. The union is
        // deliberately uncapped: this view had no bound before the second source existed, and a bound invented
        // here would drop exactly the row that tells the owner where their thought was sorted.
        foreach (var link in await links.LoadManualOutgoingAsync(entry.Id, ct))
        {
            if (link.ToId == entry.Id || !seen.Add((link.Kind, link.ToId)))
            {
                continue;
            }

            drafts.Add((link, string.Empty));
        }

        var labels = await links.LoadTargetLabelsAsync([.. drafts.Select(draft => draft.Link)], ct);
        List<EntryOutgoingLink> outgoing =
        [
            .. drafts.Select(draft =>
            {
                var label = labels.GetValueOrDefault((draft.Link.Kind, draft.Link.ToId));
                return new EntryOutgoingLink(
                    draft.Link.Kind,
                    draft.Link.ToId,
                    label?.Label ?? draft.Written,
                    label?.Slug,
                    label?.ProjectId);
            })
        ];

        var sources = await links.LoadBacklinkSourcesAsync(entry.Id, ct);
        List<EntryBacklink> incoming =
        [
            .. sources.Select(source => new EntryBacklink(
                source.Id,
                source.Title,
                BacklinkSnippet.From(source.Content, entry.Title)))
        ];

        return new EntryLinkView(outgoing, incoming, unresolved);
    }

    private static string Verbatim(WikiLinkTarget target) => target.Kind switch
    {
        EntryLinkKind.Feature => "feature:" + target.Key,
        EntryLinkKind.Project => "project:" + target.Key,
        _ => target.Key
    };

    // The dry preview the resolve-links door serves: the same outcome walk the detail view runs, minus the
    // persisted entry — no stored edges, no backlinks, and the self bucket answers instead of being dropped.
    // The anchor is the entry whose content the caller is redrafting (an append); without one the proposed
    // note does not exist yet, so nothing can be a self-reference and the bucket stays empty.
    public static async Task<EntryResolveLinksResponse> ResolveProposedAsync(
        EntryLinkResolver resolver,
        EntryLinkReader links,
        string content,
        Guid? sourceEntryId,
        CancellationToken ct)
    {
        var outcomes = await resolver.ResolveOutcomesForReadAsync(
            sourceEntryId ?? Guid.Empty, WikiLinkParser.Parse(content), ct);

        var drafts = new List<(ResolvedLink Link, string Written)>();
        var unresolved = new List<string>();
        var self = new List<string>();
        var seen = new HashSet<(EntryLinkKind, Guid)>();

        foreach (var outcome in outcomes)
        {
            if (outcome.ToId is not { } targetId)
            {
                unresolved.Add(Verbatim(outcome.Target));
                continue;
            }

            if (targetId == sourceEntryId)
            {
                self.Add(Verbatim(outcome.Target));
                continue;
            }

            if (seen.Add((outcome.Target.Kind, targetId)))
            {
                drafts.Add((new ResolvedLink(outcome.Target.Kind, targetId), outcome.Target.Key));
            }
        }

        var labels = await links.LoadTargetLabelsAsync([.. drafts.Select(draft => draft.Link)], ct);

        List<EntryOutgoingLink> resolved =
        [
            .. drafts.Select(draft =>
            {
                var label = labels.GetValueOrDefault((draft.Link.Kind, draft.Link.ToId));
                return new EntryOutgoingLink(
                    draft.Link.Kind,
                    draft.Link.ToId,
                    label?.Label ?? draft.Written,
                    label?.Slug,
                    label?.ProjectId);
            })
        ];

        return new EntryResolveLinksResponse(resolved, unresolved, self);
    }
}
