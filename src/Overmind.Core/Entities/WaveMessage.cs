using System.Text.RegularExpressions;

namespace Overmind.Core;

public sealed partial class WaveMessage
{
    public const int MaxTextLength = 500;
    public const int MaxIdentityLength = AgentSession.MaxIdentityLength;
    public const int MaxMentions = 16;
    public const int MaxMentionsLength = 600;
    public const string AllRooms = "all";
    public const string OwnerRole = "owner";

    private WaveMessage()
    {
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Room { get; private set; } = string.Empty;
    public string AuthorAlias { get; private set; } = string.Empty;
    public string AuthorRole { get; private set; } = AgentSession.DefaultRole;
    public string Text { get; private set; } = string.Empty;
    public string Mentions { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    public IReadOnlyList<string> MentionList =>
        [.. Mentions.Split(' ', StringSplitOptions.RemoveEmptyEntries)];

    public static WaveMessage Create(
        Guid projectId, string room, string authorAlias, string authorRole, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        // MaxTextLength is the soft cap; the API door truncates anything past the grace
        // ceiling before it reaches here, so this guard only catches bypasses.
        var hardCeiling = SoftCap.Ceiling(MaxTextLength);
        var trimmed = text.Trim();
        if (trimmed.Length > hardCeiling)
        {
            throw new ArgumentException(
                $"Wave message text exceeds the {hardCeiling} character hard ceiling (soft cap {MaxTextLength}).",
                nameof(text));
        }

        return new WaveMessage
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Room = NormalizeRoom(room),
            AuthorAlias = NormalizeIdentity(authorAlias),
            AuthorRole = NormalizeAuthorRole(authorRole),
            Text = trimmed,
            Mentions = FormatMentions(ParseMentions(trimmed)),
            CreatedAt = DateTime.UtcNow
        };
    }

    public static string NormalizeRoom(string room)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(room);

        var trimmed = room.Trim();
        return trimmed.Equals(AllRooms, StringComparison.OrdinalIgnoreCase)
            ? AllRooms
            : trimmed;
    }

    public static IReadOnlyList<string> ParseMentions(string text)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in MentionToken().Matches(text))
        {
            seen.Add(match.Groups[1].Value.ToLowerInvariant());
            if (seen.Count == MaxMentions)
            {
                break;
            }
        }

        return [.. seen];
    }

    private static string FormatMentions(IReadOnlyList<string> mentions) =>
        mentions.Count == 0 ? string.Empty : $" {string.Join(' ', mentions)} ";

    private static string NormalizeIdentity(string alias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);

        var trimmed = alias.Trim();
        if (trimmed.Length > MaxIdentityLength)
        {
            throw new ArgumentException(
                $"Agent identity '{trimmed}' exceeds the {MaxIdentityLength} character cap.", nameof(alias));
        }

        return trimmed;
    }

    private static string NormalizeAuthorRole(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        var trimmed = role.Trim();
        if (trimmed.Length > MaxIdentityLength)
        {
            throw new ArgumentException(
                $"Agent identity '{trimmed}' exceeds the {MaxIdentityLength} character cap.", nameof(role));
        }

        return trimmed.Equals(OwnerRole, StringComparison.Ordinal)
            ? OwnerRole
            : AgentSession.NormalizeRole(trimmed);
    }

    [GeneratedRegex(@"(?<![A-Za-z0-9_-])@([A-Za-z0-9_-]{1,32})(?![A-Za-z0-9_-])")]
    private static partial Regex MentionToken();
}
