using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Notifications;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class IssueService
{
    private readonly OvermindDbContext _db;
    private readonly PlanningMaterializationDispatcher _materialization;
    private readonly PlanningArtifactService _artifacts;
    private readonly NotificationsService _notifications;

    private readonly FeatureClaimService _claims;

    public IssueService(
        OvermindDbContext db,
        PlanningArtifactService artifacts,
        NotificationsService notifications,
        FeatureClaimService claims,
        PlanningMaterializationDispatcher materialization)
    {
        _db = db;
        _artifacts = artifacts;
        _notifications = notifications;
        _claims = claims;
        _materialization = materialization;
    }

    public async Task<Issue> CreateAsync(
        Guid projectId, Guid authorProjectId, string title, string body,
        PlanningLogSource source = PlanningLogSource.Agent, CancellationToken ct = default)
    {
        var board = await RequireProjectAsync(projectId, ct);
        var author = await RequireProjectAsync(authorProjectId, ct);

        var issue = Issue.Create(projectId, authorProjectId, title, body, source);
        _db.Issues.Add(issue);
        await _db.SaveChangesAsync(ct);

        if (authorProjectId != projectId)
        {
            await _notifications.CreateAsync(
                NotificationKind.Info,
                $"Project '{author.Name}' filed issue «{issue.Title}» on your board — it needs a fix in your code.",
                projectId,
                issue.Id.ToString(),
                "issues",
                "issues",
                ct);
        }

        return issue;
    }

    public async Task<IReadOnlyList<Issue>> ListAsync(
        Guid projectId, IssueStatus? status, Guid? authorProjectId, CancellationToken ct = default)
    {
        var issues = _db.Issues.AsNoTracking().Where(i => i.ProjectId == projectId);
        if (status.HasValue)
        {
            issues = issues.Where(i => i.Status == status.Value);
        }

        if (authorProjectId.HasValue)
        {
            issues = issues.Where(i => i.AuthorProjectId == authorProjectId.Value);
        }

        return await issues
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);
    }

    // Cross-project board order: IssueStatus is declared open → triaged → promoted → closed,
    // so the numeric sort is the open-first order the 115 spec pins.
    public async Task<IReadOnlyList<IssueBoardRow>> ListAllAsync(
        IssueStatus? status, CancellationToken ct = default)
    {
        var issues = _db.Issues.AsNoTracking();
        if (status.HasValue)
        {
            issues = issues.Where(i => i.Status == status.Value);
        }

        return await issues
            .OrderBy(i => i.Status)
            .ThenByDescending(i => i.CreatedAt)
            .Select(i => new IssueBoardRow(
                i.Id,
                i.ProjectId,
                i.Title,
                i.Status,
                i.AuthorProjectId,
                i.CreatedAt,
                i.ClosedAt,
                i.PromotedToArtifactId,
                _db.IssueAppends.Count(a => a.IssueId == i.Id)))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> AppendCountsAsync(
        IReadOnlyCollection<Guid> issueIds, CancellationToken ct = default)
    {
        var counts = await _db.IssueAppends
            .Where(a => issueIds.Contains(a.IssueId))
            .GroupBy(a => a.IssueId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);
        return counts.ToDictionary(c => c.Key, c => c.Count);
    }

    public async Task<(Issue Issue, IReadOnlyList<IssueAppend> Appends)> GetAsync(
        Guid issueId, CancellationToken ct = default)
    {
        var issue = await RequireIssueAsync(issueId, ct);
        var appends = await _db.IssueAppends
            .AsNoTracking()
            .Where(a => a.IssueId == issueId)
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .ToListAsync(ct);

        return (issue, appends);
    }

    public async Task<IssueAppend> AppendAsync(
        Guid issueId, Guid authorProjectId, string text, CancellationToken ct = default)
    {
        await RequireProjectAsync(authorProjectId, ct);
        var issue = await RequireIssueAsync(issueId, ct);
        var append = issue.Append(authorProjectId, text);
        _db.IssueAppends.Add(append);
        await _db.SaveChangesAsync(ct);
        return append;
    }

    public async Task<Issue> SetStatusAsync(
        Guid issueId, IssueStatus target, PlanningLogSource source, CancellationToken ct = default)
    {
        var issue = await RequireIssueAsync(issueId, ct);
        issue.SetStatus(target, source);
        await _db.SaveChangesAsync(ct);
        return issue;
    }

    // Lock-aware per the 028 closeout decision: a claimed target feature blocks promotion from
    // any session that does not hold the claim (foreign, any liveness — promote is not take-over).
    // sessionId null = the human door, which promotes anything.
    public async Task<(Issue Issue, PlanningArtifact Artifact)> PromoteAsync(
        Guid issueId, string featureSlug, PlanningLogSource source, Guid? sessionId = null,
        CancellationToken ct = default)
    {
        var issue = await RequireIssueAsync(issueId, ct);
        var feature = await _db.PlanningFeatures
            .FirstOrDefaultAsync(f => f.ProjectId == issue.ProjectId && f.Slug == featureSlug, ct)
            ?? throw PlanningException.MissingFeature(featureSlug);
        await _claims.EnsureHolderAsync(feature.ProjectId, feature.Slug, sessionId, ct);
        var appends = await _db.IssueAppends
            .Where(a => a.IssueId == issue.Id)
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .ToListAsync(ct);
        var body = issue.ComposeTaskBody(appends);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var artifact = await _artifacts.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, body, order: null,
            review: PlanningReviewLevel.Light,
            logNote: $"promoted from issue {issue.Id}", ct, source: source);
        issue.Promote(artifact.Id, source);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _materialization.DispatchAsync(issue.ProjectId, ct);
        return (issue, artifact);
    }

    private async Task<Project> RequireProjectAsync(Guid projectId, CancellationToken ct) =>
        await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
        ?? throw PlanningException.MissingProject(projectId);

    private async Task<Issue> RequireIssueAsync(Guid issueId, CancellationToken ct) =>
        await _db.Issues.FirstOrDefaultAsync(i => i.Id == issueId, ct)
        ?? throw PlanningException.MissingIssue(issueId);
}
