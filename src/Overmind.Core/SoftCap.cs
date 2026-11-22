using System.Text.Json.Serialization;

namespace Overmind.Core;

// Owner soft-cap formula (feature 266): at or under the limit the write is silent; under the
// ceiling it stores whole with a warning; past the ceiling it is accepted truncated to the keep
// length with a preview of the lost tail. Every capped field derives its numbers from here so no
// door re-derives the arithmetic — the grace band in particular lives only in Ceiling().
public static class SoftCap
{
    public const int PreviewChars = 50;

    public static int Keep(int limit) => limit + limit / 5;

    public static int Ceiling(int limit) => limit + limit / 5 + limit / 50;

    // Zone math only. Field-shape rules (single-line, blankness) belong to the doors and run
    // BEFORE this call — a prefix can never gain a newline, and capping must not launder one away.
    public static SoftCapOutcome Apply(string? text, int limit)
    {
        if (text is null)
        {
            return new SoftCapOutcome(null, null);
        }

        if (text.Length <= limit)
        {
            return new SoftCapOutcome(text, null);
        }

        if (text.Length <= Ceiling(limit))
        {
            return new SoftCapOutcome(text, new SoftCapWarning
            {
                AcceptedLen = text.Length,
                Limit = limit,
                Truncated = false,
            });
        }

        var stored = Cut(text, Keep(limit));
        return new SoftCapOutcome(stored, new SoftCapWarning
        {
            AcceptedLen = stored.Length,
            Limit = limit,
            Truncated = true,
            LostTailPreview = TailPreview(text[stored.Length..]),
        });
    }

    private static string Cut(string text, int keep)
    {
        var end = keep;
        if (end > 0 && end < text.Length &&
            char.IsHighSurrogate(text[end - 1]) && char.IsLowSurrogate(text[end]))
        {
            end--;
        }

        return text[..end];
    }

    private static string? TailPreview(string dropped)
    {
        if (dropped.Length == 0)
        {
            return null;
        }

        var start = Math.Max(dropped.Length - PreviewChars, 0);
        if (start > 0 && char.IsLowSurrogate(dropped[start]))
        {
            start++;
        }

        return dropped[start..];
    }
}

public sealed record SoftCapOutcome(string? Stored, SoftCapWarning? Warning);

// Frozen wire contract (266 plan): later tasks consume these four keys, nobody reshapes them.
public sealed record SoftCapWarning
{
    [JsonPropertyName("accepted_len")]
    public required int AcceptedLen { get; init; }

    [JsonPropertyName("limit")]
    public required int Limit { get; init; }

    [JsonPropertyName("truncated")]
    public required bool Truncated { get; init; }

    [JsonPropertyName("lost_tail_preview")]
    public string? LostTailPreview { get; init; }
}

// Ceilings introduced by the 266 spec table for payloads that were uncapped or validated by
// literals only; fields with a canonical entity constant (wave, brief, contour, directive)
// keep using it as the limit.
public static class SoftCapLimits
{
    public const int LogLine = 2000;
    public const int Reason = 500;
    public const int FollowUpText = 2000;
}
