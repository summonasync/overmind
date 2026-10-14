using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public static class PointerMarkerGrammar
{
    private const string Marker = "pointer";

    public static void EnsureValid(string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return;
        }

        foreach (var line in body.Split('\n'))
        {
            if (ParseLine(line).Error is { } error)
            {
                throw PlanningException.InvalidPointerMarker(line.Trim(), error);
            }
        }
    }

    public static IReadOnlyList<ArtifactPointer> Parse(string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return [];
        }

        return [.. body
            .Split('\n')
            .Select(ParseLine)
            .Where(parse => parse.Pointer is not null)
            .Select(parse => parse.Pointer!)];
    }

    public static PointerLineParse ParseLine(string line)
    {
        var trimmed = line.TrimStart();
        if (!trimmed.StartsWith(Marker, StringComparison.Ordinal))
        {
            return PointerLineParse.NotMarker;
        }

        var afterMarker = trimmed[Marker.Length..];
        if (afterMarker.Length > 0 && afterMarker[0] != ':' && !char.IsWhiteSpace(afterMarker[0]))
        {
            return PointerLineParse.NotMarker;
        }

        if (afterMarker.Length == 0 || afterMarker[0] != ':')
        {
            return PointerLineParse.Invalid("the ':' is missing right after 'pointer'.");
        }

        var value = afterMarker[1..];
        if (value.Length == 0)
        {
            return PointerLineParse.Invalid("the path after 'pointer:' is missing.");
        }

        if (!char.IsWhiteSpace(value[0]))
        {
            return PointerLineParse.Invalid("whitespace is required between 'pointer:' and the path.");
        }

        var target = value.Trim();
        if (target.Length == 0)
        {
            return PointerLineParse.Invalid("the path after 'pointer:' is missing.");
        }

        if (target.Any(char.IsWhiteSpace))
        {
            return PointerLineParse.Invalid(
                "the target must be a single token — one pointer per line, no spaces in the path or anchor.");
        }

        var hash = target.IndexOf('#');
        var path = hash < 0 ? target : target[..hash];
        if (path.Length == 0)
        {
            return PointerLineParse.Invalid("the path before '#' is missing.");
        }

        if (hash >= 0 && hash == target.Length - 1)
        {
            return PointerLineParse.Invalid("the anchor after '#' is empty.");
        }

        if (path[0] == '/')
        {
            return PointerLineParse.Invalid("the path must be repo-relative, not absolute.");
        }

        if (path.Contains('\\'))
        {
            return PointerLineParse.Invalid("backslashes are not allowed — forward slashes only.");
        }

        var segments = path.Split('/');
        if (segments.Any(segment => segment == ".."))
        {
            return PointerLineParse.Invalid("'..' segments are not allowed in a repo-relative path.");
        }

        if (segments.Any(segment => segment.Length == 0))
        {
            return PointerLineParse.Invalid("the path carries an empty segment (double or trailing slash).");
        }

        return PointerLineParse.Valid(new ArtifactPointer(path, hash < 0 ? null : target[(hash + 1)..]));
    }
}
