using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Notifications;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class IssueServiceTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly IPlanningMaterializer _materializer;
    private static readonly Guid HolderSessionId = Guid.NewGuid();
    private static readonly Guid ForeignSessionId = Guid.NewGuid();

    private readonly Guid _boardProjectId;
    private readonly Guid _authorProjectId;
    private readonly Guid _foreignProjectId;

    public IssueServiceTests()
    {
        _context = _db.CreateContext();
        _materializer = Substitute.For<IPlanningMaterializer>();
        _materializer.SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(
                Guid.Empty, PlanningMaterialization.Off, [], [], []));

        var board = Project.Create($"issues-board-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        var author = Project.Create($"issues-author-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        var foreign = Project.Create($"issues-foreign-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(board);
        _context.Projects.Add(author);
        _context.Projects.Add(foreign);
        _context.SaveChanges();
        _boardProjectId = board.Id;
        _authorProjectId = author.Id;
        _foreignProjectId = foreign.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
    }

    private IssueService CreateIssueService() =>
        new(
            _context,
            new PlanningArtifactService(
                _context,
                TestPlanningDispatcher.Create(_context, _materializer),
                new TaskDependencyService(_context, TestPlanningDispatcher.Create(_context, _materializer))),
            new NotificationsService(_context),
            new FeatureClaimService(_context, TestPlanningDispatcher.Create(_context, _materializer)),
            TestPlanningDispatcher.Create(_context, _materializer));

    private async Task<object?> ScalarRawAsync(string sql)
    {
        var connection = (System.Data.Common.DbConnection)_context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    private async Task<int> ScalarAsync(string sql) =>
        Convert.ToInt32(await ScalarRawAsync(sql));

    private PlanningFeature SeedFeature(string slug)
    {
        var feature = PlanningFeature.Create(_boardProjectId, slug, slug, "# " + slug);
        _context.PlanningFeatures.Add(feature);
        _context.SaveChanges();
        return feature;
    }

    private async Task<Issue> SeedIssueAsync(IssueStatus status = IssueStatus.Open)
    {
        var issue = await CreateIssueService().CreateAsync(
            _boardProjectId, _authorProjectId, "Crash on save", "## Repro\nOpen the file.");
        if (status == IssueStatus.Triaged)
        {
            issue.SetStatus(IssueStatus.Triaged, PlanningLogSource.Agent);
            await _context.SaveChangesAsync();
        }

        return issue;
    }

    [Fact]
    public async Task Create_PersistsOpenIssueWithProvenance()
    {
        var issue = await CreateIssueService()
            .CreateAsync(_boardProjectId, _authorProjectId, "Crash on save", "Steps.");

        issue.Status.ShouldBe(IssueStatus.Open);
        issue.ProjectId.ShouldBe(_boardProjectId);
        issue.AuthorProjectId.ShouldBe(_authorProjectId);

        var persisted = await _context.Issues.AsNoTracking().SingleAsync(i => i.Id == issue.Id);
        persisted.Title.ShouldBe("Crash on save");
        persisted.AuthorProjectId.ShouldBe(_authorProjectId);
    }

    [Fact]
    public async Task Create_UnknownAuthorProject_ThrowsNotFound()
    {
        var ex = await Should.ThrowAsync<PlanningException>(() => CreateIssueService()
            .CreateAsync(_boardProjectId, Guid.NewGuid(), "Crash on save", "Steps."));

        ex.Kind.ShouldBe(PlanningFailureKind.NotFound);
        ex.Code.ShouldBe(PlanningFailureCode.MissingProject);
    }

    [Fact]
    public async Task Create_UnknownBoardProject_ThrowsNotFound()
    {
        var ex = await Should.ThrowAsync<PlanningException>(() => CreateIssueService()
            .CreateAsync(Guid.NewGuid(), _authorProjectId, "Crash on save", "Steps."));

        ex.Kind.ShouldBe(PlanningFailureKind.NotFound);
    }

    [Fact]
    public async Task Append_LandsWithProvenanceFromAnyProject()
    {
        var issue = await SeedIssueAsync();

        var append = await CreateIssueService()
            .AppendAsync(issue.Id, _foreignProjectId, "worse on nightly");

        append.IssueId.ShouldBe(issue.Id);
        append.AuthorProjectId.ShouldBe(_foreignProjectId);
        var persisted = await _context.IssueAppends.AsNoTracking()
            .Where(a => a.IssueId == issue.Id)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();
        persisted.Count.ShouldBe(1);
        persisted[0].Text.ShouldBe("worse on nightly");
    }

    [Fact]
    public async Task Append_UnknownAuthorProject_ThrowsNotFound()
    {
        var issue = await SeedIssueAsync();

        var ex = await Should.ThrowAsync<PlanningException>(() => CreateIssueService()
            .AppendAsync(issue.Id, Guid.NewGuid(), "text"));

        ex.Kind.ShouldBe(PlanningFailureKind.NotFound);
        ex.Code.ShouldBe(PlanningFailureCode.MissingProject);
    }

    [Fact]
    public async Task Create_UiSource_RecordedOnTheIssue()
    {
        var issue = await CreateIssueService()
            .CreateAsync(_boardProjectId, _authorProjectId, "Crash on save", "Steps.", PlanningLogSource.Ui);

        issue.StatusSource.ShouldBe(PlanningLogSource.Ui);
        var persisted = await _context.Issues.AsNoTracking().SingleAsync(i => i.Id == issue.Id);
        persisted.StatusSource.ShouldBe(PlanningLogSource.Ui);
    }

    [Fact]
    public async Task Append_UnknownIssue_ThrowsNotFound()
    {
        var ex = await Should.ThrowAsync<PlanningException>(() => CreateIssueService()
            .AppendAsync(Guid.NewGuid(), _authorProjectId, "text"));

        ex.Kind.ShouldBe(PlanningFailureKind.NotFound);
        ex.Code.ShouldBe(PlanningFailureCode.MissingIssue);
    }

    [Fact]
    public async Task SetStatus_ForwardChain_PersistsSourceMarker()
    {
        var issue = await SeedIssueAsync();

        var moved = await CreateIssueService()
            .SetStatusAsync(issue.Id, IssueStatus.Triaged, PlanningLogSource.Ui);

        moved.Status.ShouldBe(IssueStatus.Triaged);
        moved.StatusSource.ShouldBe(PlanningLogSource.Ui);
        var persisted = await _context.Issues.AsNoTracking().SingleAsync(i => i.Id == issue.Id);
        persisted.Status.ShouldBe(IssueStatus.Triaged);
        persisted.StatusSource.ShouldBe(PlanningLogSource.Ui);
    }

    [Fact]
    public async Task SetStatus_NoReopen_Throws()
    {
        var issue = await SeedIssueAsync();
        var service = CreateIssueService();
        await service.SetStatusAsync(issue.Id, IssueStatus.Closed, PlanningLogSource.Agent);

        var ex = await Should.ThrowAsync<PlanningException>(() => service
            .SetStatusAsync(issue.Id, IssueStatus.Open, PlanningLogSource.Agent));

        ex.Code.ShouldBe(PlanningFailureCode.IssueInvalidTransition);
    }

    [Fact]
    public async Task Promote_ComposesTaskArtifactAndLinksBothWays()
    {
        var issue = await SeedIssueAsync();
        await CreateIssueService().AppendAsync(issue.Id, _authorProjectId, "still reproducible");
        SeedFeature("050-crash-fix");

        var (promoted, artifact) = await CreateIssueService()
            .PromoteAsync(issue.Id, "050-crash-fix", PlanningLogSource.Agent);

        promoted.Status.ShouldBe(IssueStatus.Promoted);
        promoted.PromotedToArtifactId.ShouldBe(artifact.Id);

        var persistedArtifact = await _context.PlanningArtifacts.AsNoTracking()
            .Include(a => a.Versions)
            .SingleAsync(a => a.Id == artifact.Id);
        persistedArtifact.FeatureId.ShouldBe(featureId());
        persistedArtifact.Kind.ShouldBe(PlanningArtifactKind.Task);
        persistedArtifact.Body.ShouldContain("<goal>");
        persistedArtifact.Body.ShouldContain("Crash on save");
        persistedArtifact.Body.ShouldContain("<scope>");
        persistedArtifact.Body.ShouldContain("- Repro: Open the file.");
        persistedArtifact.Body.ShouldContain("<obstacles>");
        persistedArtifact.Body.ShouldContain("still reproducible");
        persistedArtifact.Body.ShouldContain("<refs>");
        persistedArtifact.Body.ShouldContain($"Issue {issue.Id}");

        var persistedIssue = await _context.Issues.AsNoTracking().SingleAsync(i => i.Id == issue.Id);
        persistedIssue.PromotedToArtifactId.ShouldBe(artifact.Id);
        persistedIssue.Status.ShouldBe(IssueStatus.Promoted);

        Guid featureId() => _context.PlanningFeatures
            .AsNoTracking()
            .Single(f => f.Slug == "050-crash-fix")
            .Id;
    }

    [Fact]
    public async Task Promote_ClaimFreeFeature_Promotes()
    {
        var issue = await SeedIssueAsync();
        SeedFeature("050-crash-fix");

        var (_, artifact) = await CreateIssueService()
            .PromoteAsync(issue.Id, "050-crash-fix", PlanningLogSource.Agent);

        artifact.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Promote_ForeignLiveClaim_Returns409AndWritesNothing()
    {
        var issue = await SeedIssueAsync();
        SeedFeature("050-crash-fix");
        await _context.IssueAppends.AddAsync(IssueAppend.Create(issue.Id, _authorProjectId, "trail entry"));
        await _context.SaveChangesAsync();
        var claims = new FeatureClaimService(_context, TestPlanningDispatcher.Create(_context, _materializer));
        await claims.ClaimAsync(_boardProjectId, "050-crash-fix", "Dot", "coder", HolderSessionId);
        var artifactsBefore = await ScalarAsync("SELECT COUNT(*) FROM PlanningArtifacts");

        var service = CreateIssueService();
        var ex = await Should.ThrowAsync<PlanningException>(() => service.PromoteAsync(
            issue.Id, "050-crash-fix", PlanningLogSource.Agent, ForeignSessionId));

        ex.Code.ShouldBe(PlanningFailureCode.FeatureClaimHeld);
        ex.Message.ShouldContain("claim held by Dot by coder (alive)");
        var artifactsAfter = await ScalarAsync("SELECT COUNT(*) FROM PlanningArtifacts");
        artifactsAfter.ShouldBe(artifactsBefore);
        (await _context.Issues.AsNoTracking().SingleAsync(i => i.Id == issue.Id))
            .Status.ShouldBe(IssueStatus.Open);
    }

    [Fact]
    public async Task Promote_OwnSessionClaim_Promotes()
    {
        var issue = await SeedIssueAsync();
        SeedFeature("050-crash-fix");
        var claims = new FeatureClaimService(_context, TestPlanningDispatcher.Create(_context, _materializer));
        await claims.ClaimAsync(_boardProjectId, "050-crash-fix", "Dan", "qwen", HolderSessionId);

        var (promoted, artifact) = await CreateIssueService()
            .PromoteAsync(issue.Id, "050-crash-fix", PlanningLogSource.Agent, HolderSessionId);

        promoted.Status.ShouldBe(IssueStatus.Promoted);
        promoted.PromotedToArtifactId.ShouldBe(artifact.Id);
    }

    [Fact]
    public async Task Promote_ForeignEndedClaim_StillRefused_NoTakeOver()
    {
        var issue = await SeedIssueAsync();
        SeedFeature("050-crash-fix");
        var claims = new FeatureClaimService(_context, TestPlanningDispatcher.Create(_context, _materializer));
        await claims.ClaimAsync(_boardProjectId, "050-crash-fix", "Dan", "qwen", HolderSessionId);
        await claims.UnclaimAsync(_boardProjectId, "050-crash-fix", HolderSessionId);
        await claims.ClaimAsync(_boardProjectId, "050-crash-fix", "Dan", "qwen", HolderSessionId);
        var held = await _context.AgentSessions.SingleAsync(s => s.Id == HolderSessionId);
        _context.AgentSessions.Remove(held);
        _context.AgentSessions.Add(AgentSession.Restore(
            HolderSessionId, "Dan", "qwen",
            DateTime.UtcNow.AddHours(-3), DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1)));
        await _context.SaveChangesAsync();

        var service = CreateIssueService();
        var ex = await Should.ThrowAsync<PlanningException>(() => service.PromoteAsync(
            issue.Id, "050-crash-fix", PlanningLogSource.Agent, ForeignSessionId));

        ex.Message.ShouldContain("claim held by Dan by qwen (ended)");
    }

    [Fact]
    public async Task Promote_UnknownFeature_ThrowsNotFound()
    {
        var issue = await SeedIssueAsync();

        var ex = await Should.ThrowAsync<PlanningException>(() => CreateIssueService()
            .PromoteAsync(issue.Id, "no-such-feature", PlanningLogSource.Agent));

        ex.Kind.ShouldBe(PlanningFailureKind.NotFound);
        ex.Code.ShouldBe(PlanningFailureCode.MissingFeature);
    }

    [Fact]
    public async Task Promote_FromPromoted_Throws()
    {
        var issue = await SeedIssueAsync();
        SeedFeature("050-crash-fix");
        var service = CreateIssueService();
        await service.PromoteAsync(issue.Id, "050-crash-fix", PlanningLogSource.Agent);

        var ex = await Should.ThrowAsync<PlanningException>(() => service
            .PromoteAsync(issue.Id, "050-crash-fix", PlanningLogSource.Agent));

        ex.Code.ShouldBe(PlanningFailureCode.IssueInvalidTransition);
    }
}
