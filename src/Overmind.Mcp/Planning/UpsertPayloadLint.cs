using ModelContextProtocol;

namespace Overmind.Mcp.Planning;

// Client-side echo of the host's pointer grammar (Infrastructure/PointerMarkerGrammar): the
// adapter stays project-reference-free, so the accepted form is duplicated here on purpose —
// a mismatch would show up as a rejected dryRun on the server, never as a silent accept.
internal static class UpsertPayloadLint
{
    private const string Marker = "pointer";

    internal static void PointerLines(string field, string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return;
        }

        foreach (var line in body.Split('\n'))
        {
            if (PointerError(line) is { } error)
            {
                throw new McpException(
                    $"{field}: malformed pointer line '{line.Trim()}' — {error} " +
                    "A pointer line is 'pointer: <repo-relative-path>[#anchor]': the case-sensitive " +
                    "marker 'pointer:' as the first token, whitespace, then a single-token repo-relative " +
                    "path (no leading '/', forward slashes only, no '..' segments) with an optional " +
                    "non-empty '#anchor'. Example: 'pointer: src/Overmind.Core/Entities/PlanningArtifact.cs#L81'.");
            }
        }
    }

    private static string? PointerError(string line)
    {
        var trimmed = line.TrimStart();
        if (!trimmed.StartsWith(Marker, StringComparison.Ordinal))
        {
            return null;
        }

        var afterMarker = trimmed[Marker.Length..];
        if (afterMarker.Length > 0 && afterMarker[0] != ':' && !char.IsWhiteSpace(afterMarker[0]))
        {
            return null;
        }

        if (afterMarker.Length == 0 || afterMarker[0] != ':')
        {
            return "the ':' is missing right after 'pointer'.";
        }

        var value = afterMarker[1..];
        if (value.Length == 0)
        {
            return "the path after 'pointer:' is missing.";
        }

        if (!char.IsWhiteSpace(value[0]))
        {
            return "whitespace is required between 'pointer:' and the path.";
        }

        var target = value.Trim();
        if (target.Length == 0)
        {
            return "the path after 'pointer:' is missing.";
        }

        if (target.Any(char.IsWhiteSpace))
        {
            return "the target must be a single token — one pointer per line, no spaces in the path or anchor.";
        }

        var hash = target.IndexOf('#');
        var path = hash < 0 ? target : target[..hash];
        if (path.Length == 0)
        {
            return "the path before '#' is missing.";
        }

        if (hash >= 0 && hash == target.Length - 1)
        {
            return "the anchor after '#' is empty.";
        }

        if (path[0] == '/')
        {
            return "the path must be repo-relative, not absolute.";
        }

        if (path.Contains('\\'))
        {
            return "backslashes are not allowed — forward slashes only.";
        }

        var segments = path.Split('/');
        if (segments.Any(segment => segment == ".."))
        {
            return "'..' segments are not allowed in a repo-relative path.";
        }

        return segments.Any(segment => segment.Length == 0)
            ? "the path carries an empty segment (double or trailing slash)."
            : null;
    }
}
