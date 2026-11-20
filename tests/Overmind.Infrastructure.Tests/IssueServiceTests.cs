using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Notifications;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Overmind.Infrastructure.Wake;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class IssueServiceTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly IPlanningMaterializer _materializer;
    private readonly WakeDropFanout _wakeFanout;
    private readonly ServiceProvider _wakeServices;
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
        (_wakeFanout, _wakeServices) = TestWakeFanout.Create(_db);

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
        _wakeServices.Dispose();
        _context.Dispose();
        _db.Dispose();
    }

    private IssueService CreateIssueService() => CreateIssueService(_context);

    private IssueService CreateIssueService(OvermindDbContext context) =>
        new(
            context,
            new PlanningArtifactService(
                context,
                TestPlanningDispatcher.Create(context, _materializer),
                new TaskDependencyService(context, TestPlanningDispatcher.Create(context, _materializer))),
            new NotificationsService(
                context,
                new ReplyAutoActFanout(
                    context,
                    new ReplyAutoActQueue(),
                    NullLogger<ReplyAutoActFanout>.Instance),
                _wakeFanout,
                []),
            new FeatureClaimService(context, TestPlanningDispatcher.Create(context, _materializer)),
            TestPlanningDispatcher.Create(context, _materializer));

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
    public async Task Create_RequestKind_PersistsKind()
    {
        var issue = await CreateIssueService()
            .CreateAsync(_boardProjectId, _authorProjectId, "Add dark mode", "Pretty please.", kind: Issue.RequestKind);

        issue.Kind.ShouldBe(Issue.RequestKind);
        var persisted = await _context.Issues.AsNoTracking().SingleAsync(i => i.Id == issue.Id);
        persisted.Kind.ShouldBe(Issue.RequestKind);
    }

    [Fact]
    public async Task Create_JunkKind_NormalizesToIssue()
    {
        var issue = await CreateIssueService()
            .CreateAsync(_boardProjectId, _authorProjectId, "Crash on save", "Steps.", kind: "banana");

        issue.Kind.ShouldBe(Issue.DefaultKind);
    }

    [Fact]
    public async Task List_KindFilter_ReturnsOnlyMatchingKind()
    {
        var service = CreateIssueService();
        var plain = await service.CreateAsync(_boardProjectId, _authorProjectId, "Crash on save", "body");
        var request = await service.CreateAsync(
            _boardProjectId, _authorProjectId, "Add dark mode", "body", kind: Issue.RequestKind);

        var requests = await CreateIssueService().ListAsync(
            _boardProjectId, status: null, authorProjectId: null, Issue.RequestKind);
        var issues = await CreateIssueService().ListAsync(
            _boardProjectId, status: null, authorProjectId: null, Issue.DefaultKind);
        var all = await CreateIssueService().ListAsync(
            _boardProjectId, status: null, authorProjectId: null, kind: null);

        requests.Select(i => i.Id).ShouldBe([request.Id]);
        issues.Select(i => i.Id).ShouldBe([plain.Id]);
        all.Select(i => i.Id).ShouldBe([request.Id, plain.Id]);
    }

    [Fact]
    public async Task ListAll_KindFilter_FiltersCrossProjectBoard()
    {
        var service = CreateIssueService();
        await service.CreateAsync(_boardProjectId, _authorProjectId, "Crash on save", "body");
        var request = await CreateIssueService()
            .CreateAsync(_foreignProjectId, _authorProjectId, "Add dark mode", "body", kind: Issue.RequestKind);

        var rows = await CreateIssueService().ListAllAsync(null, Issue.RequestKind);

        rows.Select(r => r.Id).ShouldBe([request.Id]);
        rows.Single().Kind.ShouldBe(Issue.RequestKind);
    }

    [Fact]
    public async Task Promote_FromRequestKindCard_Promotes()
    {
        var issue = await CreateIssueService().CreateAsync(
            _boardProjectId, _authorProjectId, "Add dark mode", "Pretty please.", kind: Issue.RequestKind);
        SeedFeature("050-dark-mode");

        var (promoted, artifact) = await CreateIssueService()
            .PromoteAsync(issue.Id, "050-dark-mode", PlanningLogSource.Agent);

        promoted.Kind.ShouldBe(Issue.RequestKind);
        promoted.Status.ShouldBe(IssueStatus.Promoted);
        promoted.PromotedToArtifactId.ShouldBe(artifact.Id);
        artifact.Body.ShouldContain($"- Request {issue.Id}");
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
    public async Task ListAll_AggregatesAcrossProjects_WithAppendCounts()
    {
        var service = CreateIssueService();
        var onBoard = await service.CreateAsync(_boardProjectId, _authorProjectId, "Board crash", "body");
        var onForeign = await CreateIssueService()
            .CreateAsync(_foreignProjectId, _authorProjectId, "Foreign crash", "body");
        await service.AppendAsync(onBoard.Id, _authorProjectId, "one follow-up");

        var rows = await CreateIssueService().ListAllAsync(null);

        rows.Select(r => r.Id).ShouldBe([onForeign.Id, onBoard.Id]);
        var boardRow = rows.Single(r => r.Id == onBoard.Id);
        boardRow.ProjectId.ShouldBe(_boardProjectId);
        boardRow.AppendCount.ShouldBe(1);
        var foreignRow = rows.Single(r => r.Id == onForeign.Id);
        foreignRow.ProjectId.ShouldBe(_foreignProjectId);
    }

    [Fact]
    public async Task ListAll_OpenFirstNewestWithinGroup_PinsBoardOrder()
    {
        var service = CreateIssueService();
        await service.CreateAsync(_boardProjectId, _authorProjectId, "oldest open", "body");
        var triaged = await service.CreateAsync(_boardProjectId, _authorProjectId, "triaged one", "body");
        var newestOpen = await service.CreateAsync(_boardProjectId, _authorProjectId, "newest open", "body");
        var newestClosed = await service.CreateAsync(_boardProjectId, _authorProjectId, "closed newest", "body");
        await service.SetStatusAsync(triaged.Id, IssueStatus.Triaged, PlanningLogSource.Agent);
        await service.SetStatusAsync(newestClosed.Id, IssueStatus.Closed, PlanningLogSource.Agent);

        var rows = await CreateIssueService().ListAllAsync(null);

        rows.Select(r => r.Title).ShouldBe(
            ["newest open", "oldest open", "triaged one", "closed newest"]);
        rows.Single(r => r.Title == "closed newest").Status.ShouldBe(IssueStatus.Closed);
    }

    [Fact]
    public async Task ListAll_StatusFilter_ReturnsOnlyMatchingStatus()
    {
        var service = CreateIssueService();
        await service.CreateAsync(_boardProjectId, _authorProjectId, "first open", "body");
        var triaged = await service.CreateAsync(_boardProjectId, _authorProjectId, "triaged one", "body");
        var closed = await service.CreateAsync(_boardProjectId, _authorProjectId, "closed one", "body");
        await service.CreateAsync(_boardProjectId, _authorProjectId, "second open", "body");
        await service.SetStatusAsync(triaged.Id, IssueStatus.Triaged, PlanningLogSource.Agent);
        await service.SetStatusAsync(closed.Id, IssueStatus.Closed, PlanningLogSource.Agent);

        var open = await CreateIssueService().ListAllAsync(IssueStatus.Open);
        open.Select(r => r.Title).ShouldBe(["second open", "first open"]);
        var closedRows = await CreateIssueService().ListAllAsync(IssueStatus.Closed);
        closedRows.Select(r => r.Title).ShouldBe(["closed one"]);
    }

    [Fact]
    public async Task ListAll_NoIssues_ReturnsEmpty()
    {
        (await CreateIssueService().ListAllAsync(null)).ShouldBeEmpty();
        (await CreateIssueService().ListAllAsync(IssueStatus.Open)).ShouldBeEmpty();
    }

    [Fact]
    public async Task ListAll_RunsAsSingleQuery_NoRowWiseProjectLookups()
    {
        var service = CreateIssueService();
        await service.CreateAsync(_boardProjectId, _authorProjectId, "Crash on save", "body");
        await service.CreateAsync(_foreignProjectId, _authorProjectId, "Foreign crash", "body");

        var counter = new CommandCounter();
        await using var context = _db.CreateContext(counter);
        var rows = await CreateIssueService(context).ListAllAsync(null);

        rows.Count.ShouldBe(2);
        counter.Queries.ShouldBe(1);
    }

    // 246 T2 / R6: the caller-scope comparison must not read the issue twice. Each door runs twice over
    // the same card - once with no caller (the pre-246 shape) and once with the owning project id - and
    // the command counts must be equal. The baseline is asserted non-zero in the same test, so equality
    // measured over nothing cannot pass.
    [Theory]
    [InlineData("get", 2)]
    [InlineData("append", 3)]
    [InlineData("status", 2)]
    public async Task CallerScopeComparison_AddsNoQueryToIssueDoors(string door, int baseline)
    {
        // A fresh card per run: the mutating doors would otherwise compare a first transition against a
        // second one that has nothing left to do, and the counts would differ for a reason that has
        // nothing to do with the scope guard.
        var unscoped = await CountAsync((await SeedIssueAsync()).Id, door, null);
        var scoped = await CountAsync((await SeedIssueAsync()).Id, door, _boardProjectId);

        // Pinned absolutely, not just compared: equality alone passes when a query is added to BOTH
        // shapes, which is exactly how a first draft of this test was vacuous (found by an A-B control
        // that injected an extra query into the guard and stayed green).
        unscoped.ShouldBe(baseline, $"{door}: baseline measured with the scope guard disabled");
        scoped.ShouldBe(baseline, $"{door}: the scope comparison must not add a query");
    }

    private async Task<int> CountAsync(Guid issueId, string door, Guid? callerProjectId)
    {
        var counter = new ScopeCommandCounter();
        await using var context = _db.CreateContext(counter);

        await RunAsync(CreateIssueService(context), issueId, door, callerProjectId);
        return counter.Commands;
    }

    // The claim is "the guard adds no query", and the cheapest way a scope guard grows one is an
    // existence check - Any/Count - which EF runs as a SCALAR, not a reader. Counting readers only
    // would leave that invisible, so this counter covers all six command shapes.
    private sealed class ScopeCommandCounter : DbCommandInterceptor
    {
        public int Commands { get; private set; }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Commands++;
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands++;
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            Commands++;
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Commands++;
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        {
            Commands++;
            return result;
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            Commands++;
            return ValueTask.FromResult(result);
        }
    }

    private async Task RunAsync(
        IssueService service, Guid issueId, string door, Guid? callerProjectId)
    {
        switch (door)
        {
            case "get":
                await service.GetAsync(issueId, CancellationToken.None, callerProjectId);
                break;
            case "append":
                await service.AppendAsync(issueId, _authorProjectId, "note", CancellationToken.None, callerProjectId);
                break;
            case "status":
                await service.SetStatusAsync(
                    issueId, IssueStatus.Triaged, PlanningLogSource.Agent, CancellationToken.None, callerProjectId);
                break;
        }
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int Queries { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Queries++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
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
