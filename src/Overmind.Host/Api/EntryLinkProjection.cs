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
        var outcomes = await resolver.ResolveOutcomesAsync(
            entry.Id, WikiLinkParser.Parse(entry.Content), ensureJournalProject: false, ct);

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

        var labels = await links.LoadTargetLabelsAsync([.. drafts.Select(draft => draft.Link)], ct);
        List<EntryOutgoingLink> outgoing =
        [
            .. drafts.Select(draft => new EntryOutgoingLink(
                draft.Link.Kind,
                draft.Link.ToId,
                labels.GetValueOrDefault((draft.Link.Kind, draft.Link.ToId)) ?? draft.Written))
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
}
