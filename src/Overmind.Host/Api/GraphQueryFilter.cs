using Overmind.Core;
using Overmind.Infrastructure.Entries;

namespace Overmind.Host.Api;

// The query string of GET /api/graph, read once into the projection's request. A value the projection cannot honour
// answers with a named 400 instead of a silently empty field — the same posture the stream filter took for linked/
// unlinked. Widening is only ever the explicit include-done flag; nothing on this list can grow the node universe.
internal static class GraphQueryFilter
{
    public static IResult? ValidateEntryTypes(string[]? types, out IReadOnlyList<EntryType>? entryTypes)
    {
        entryTypes = null;

        var requested = (types ?? [])
            .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (requested.Length == 0)
        {
            return null;
        }

        var narrowed = new List<EntryType>(requested.Length);
        foreach (var value in requested)
        {
            if (!Enum.TryParse(value, ignoreCase: true, out EntryType type)
                || !GraphReadRequest.NodeEntryTypes.Contains(type))
            {
                return HttpErrors.BadRequest(
                    $"Unknown type filter '{value}'. Valid values: journal, decision, idea.",
                    "UnknownGraphTypeFilter");
            }

            narrowed.Add(type);
        }

        entryTypes = narrowed;
        return null;
    }

    public static IResult? ValidateFlag(string? value, string parameter, out bool flag)
    {
        var text = value?.Trim();
        flag = false;

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (text.Equals("true", StringComparison.OrdinalIgnoreCase) || text == "1")
        {
            flag = true;
            return null;
        }

        if (text.Equals("false", StringComparison.OrdinalIgnoreCase) || text == "0")
        {
            return null;
        }

        return HttpErrors.BadRequest(
            $"'{value}' is not a boolean; valid values for {parameter}: true, false.",
            "InvalidGraphFlag");
    }

    // A focus arrives as '<kind>:<id>'. Its presence forces a positive depth (the projection only windows when
    // depth is above zero, so a zero or absent depth used to mean "ignore the focus"); depth on its own is
    // meaningless without a focus. Both refusals are named so the UI never has to guess what it asked for.
    public static IResult? ValidateFocus(
        string? focus, string? depth, out GraphNodeId? node, out int? hops, out bool requested)
    {
        node = null;
        hops = null;
        requested = false;

        var handle = focus?.Trim();
        var depthText = depth?.Trim();

        if (string.IsNullOrEmpty(handle) && !string.IsNullOrEmpty(depthText))
        {
            return HttpErrors.BadRequest(
                "'depth' only means something alongside 'focus'.",
                "DepthWithoutFocus");
        }

        if (string.IsNullOrEmpty(handle))
        {
            return null;
        }

        requested = true;
        var separator = handle.IndexOf(':');
        if (separator <= 0 || separator == handle.Length - 1)
        {
            return InvalidFocus(handle);
        }

        var kindText = handle[..separator];
        var idText = handle[(separator + 1)..];

        // A task id is an edge target, never a node: the field will not draw one, so it is not a focus either.
        EntryLinkKind? kind = kindText.ToLowerInvariant() switch
        {
            "entry" => EntryLinkKind.Entry,
            "project" => EntryLinkKind.Project,
            "feature" => EntryLinkKind.Feature,
            _ => null,
        };

        if (kind is not { } nodeKind || !Guid.TryParse(idText, out var id))
        {
            return InvalidFocus(handle);
        }

        node = new GraphNodeId(nodeKind, id);

        if (string.IsNullOrEmpty(depthText))
        {
            hops = 1;
            return null;
        }

        if (!int.TryParse(depthText, out var depthValue) || depthValue < 1)
        {
            return HttpErrors.BadRequest(
                $"'depth' must be a whole number of one or more hops; got '{depth}'.",
                "InvalidGraphDepth");
        }

        hops = depthValue;
        return null;
    }

    public static IResult? ValidateSearch(string? search, out string? term)
    {
        term = search?.Trim();
        if (string.IsNullOrEmpty(term))
        {
            term = null;
            return null;
        }

        return null;
    }

    private static IResult InvalidFocus(string handle) =>
        HttpErrors.BadRequest(
            $"'focus' must be a node handle like 'entry:<id>', 'project:<id>' or 'feature:<id>'; got '{handle}'.",
            "InvalidGraphFocus");
}
