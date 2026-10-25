using System.Collections.Frozen;

namespace Overmind.Core;

public sealed class Issue
{
    public const string DefaultKind = "issue";
    public const string RequestKind = "request";
    public const int MaxKindLength = 16;

    private static readonly FrozenDictionary<IssueStatus, IssueStatus[]> AllowedTransitions =
        new Dictionary<IssueStatus, IssueStatus[]>
        {
            [IssueStatus.Open] = [IssueStatus.Triaged, IssueStatus.Closed],
            [IssueStatus.Triaged] = [IssueStatus.Closed],
            [IssueStatus.Promoted] = [IssueStatus.Closed],
            [IssueStatus.Closed] = [],
        }.ToFrozenDictionary();

    private Issue()
    {
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string Kind { get; private set; } = DefaultKind;
    public IssueStatus Status { get; private set; }
    public Guid AuthorProjectId { get; private set; }
    public PlanningLogSource StatusSource { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public Guid? PromotedToArtifactId { get; private set; }

    public static Issue Create(
        Guid projectId, Guid authorProjectId, string title, string body,
        PlanningLogSource source = PlanningLogSource.Agent, string kind = DefaultKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        return new Issue
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = title.Trim(),
            Body = body.Trim(),
            Kind = NormalizeKind(kind),
            Status = IssueStatus.Open,
            AuthorProjectId = authorProjectId,
            StatusSource = source,
            CreatedAt = DateTime.UtcNow
        };
    }

    public IssueAppend Append(Guid authorProjectId, string text) =>
        IssueAppend.Create(Id, authorProjectId, text);

    public void SetStatus(IssueStatus target, PlanningLogSource source)
    {
        if (target == Status)
        {
            return;
        }

        if (!AllowedTransitions[Status].Contains(target))
        {
            throw PlanningException.IssueInvalidTransition(Status, target);
        }

        Status = target;
        StatusSource = source;
        if (target == IssueStatus.Closed)
        {
            ClosedAt = DateTime.UtcNow;
        }
    }

    public void Promote(Guid artifactId, PlanningLogSource source)
    {
        if (Status is not (IssueStatus.Open or IssueStatus.Triaged))
        {
            throw PlanningException.IssueInvalidTransition(Status, IssueStatus.Promoted);
        }

        Status = IssueStatus.Promoted;
        StatusSource = source;
        PromotedToArtifactId = artifactId;
    }

    public static string NormalizeKind(string kind)
    {
        var trimmed = kind?.Trim();
        return trimmed is not null && trimmed.Equals(RequestKind, StringComparison.Ordinal)
            ? RequestKind
            : DefaultKind;
    }

    public string ComposeTaskBody(IReadOnlyList<IssueAppend> appends)
    {
        var (repro, expected, actual) = (ExtractSection(Body, "repro"), ExtractSection(Body, "expected"), ExtractSection(Body, "actual"));
        var scope = repro is null && expected is null && actual is null
            ? "(no Repro/Expected/Actual sections in the issue body — the full body is quoted in goal)"
            : string.Join("\n", Section("- Repro: ", repro), Section("- Expected: ", expected), Section("- Actual: ", actual));
        var trail = appends.Count == 0
            ? "(no follow-ups)"
            : string.Join("\n", appends.Select(a => $"- {a.CreatedAt:yyyy-MM-dd HH:mm} UTC — {a.Text}"));

        return $"""
            <goal>
            {Title}

            {Body}
            </goal>

            <scope>
            {scope}
            </scope>

            <obstacles>
            {trail}
            </obstacles>

            <refs>
            - {KindLabel} {Id} «{Title}» — filed by project {AuthorProjectId} on the Issues Board of project {ProjectId}; follow-ups live in the Issues Board append trail.
            </refs>
            """;
    }

    private string KindLabel => Kind == RequestKind ? "Request" : "Issue";

    private static string Section(string prefix, string? content) =>
        content is null ? $"{prefix}(not provided)" : $"{prefix}{content.Trim()}";

    private static string? ExtractSection(string body, string sectionName)
    {
        var lines = body.Split('\n');
        string? current = null;
        var collected = new List<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            var heading = line.StartsWith("## ", StringComparison.OrdinalIgnoreCase)
                ? line[3..].Trim()
                : null;

            if (heading is not null)
            {
                if (current == sectionName)
                {
                    break;
                }

                current = string.Equals(heading, sectionName, StringComparison.OrdinalIgnoreCase)
                    ? sectionName
                    : null;
                collected.Clear();
                continue;
            }

            if (current == sectionName && line.Length > 0)
            {
                collected.Add(line);
            }
        }

        return collected.Count == 0 ? null : string.Join('\n', collected);
    }
}
