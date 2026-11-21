using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Entries;

// Triage is the exit door of the orphan pile, so the fixture is built the way the owner's journal is built: a note
// in the reserved Journal container, board work in another project, and the note naming the feature it grew out of.
// Every assertion here is about what survives the act — the note unchanged, the edge present, the artifact present —
// and what does not: a half-written promotion.
public sealed class TriagePromotionTests : IDisposable
{
    private readonly TempDatabase _temp = new();
    private readonly IPlanningMaterializer _materializer = Substitute.For<IPlanningMaterializer>();
    private readonly PlanningBatchScope _batch = new();
    private readonly OvermindDbContext _context;

    private readonly Guid _journalProjectId;
    private readonly Guid _boardProjectId;
    private readonly Guid _featureId;
    private readonly Guid _noteId;

    public TriagePromotionTests()
    {
        _context = _temp.CreateContext();

        var journal = Project.Create("Journal", "/tmp/triage-promo/journal");
        var board = Project.Create("board", "/tmp/triage-promo/board");
        var feature = PlanningFeature.Create(
            board.Id, "077-target", "Target Feature", "the card the note already names");
        var note = ContextEntry.Create(
            journal.Id, EntryType.Journal, "A thought", "Worth board work, spun off [[feature:077-target]].");
        _context.Projects.AddRange(journal, board);
        _context.PlanningFeatures.Add(feature);
        _context.ContextEntries.Add(note);
        _context.SaveChanges();

        _journalProjectId = journal.Id;
        _boardProjectId = board.Id;
        _featureId = feature.Id;
        _noteId = note.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        _temp.Dispose();
    }

    [Fact]
    public async Task PromoteToFeature_CreatesTheCardAndTheManualEdge_LeavingTheNoteByteIdentical()
    {
        var before = await SnapshotNoteAsync(_context);

        var result = await Service(_context)
            .PromoteToFeatureAsync(_noteId, _boardProjectId, "Triage target", "body of the new card");

        result.TargetKind.ShouldBe(EntryLinkKind.Feature);
        var feature = await _context.PlanningFeatures.AsNoTracking()
            .SingleAsync(row => row.Id == result.TargetId);
        feature.Title.ShouldBe("Triage target");
        feature.ProjectId.ShouldBe(_boardProjectId);
        result.FeatureSlug.ShouldBe(feature.Slug);
        result.FeatureId.ShouldBe(feature.Id);

        var edge = (await EdgesAsync(_context)).Single(row => row.Origin == EntryLinkOrigin.Manual);
        edge.ToKind.ShouldBe(EntryLinkKind.Feature);
        edge.ToId.ShouldBe(feature.Id);

        (await SnapshotNoteAsync(_context)).ShouldBe(before);
    }

    [Fact]
    public async Task PromoteToTask_CreatesTheTaskUnderTheNamedFeature_AndLinksTheTaskNotItsParent()
    {
        var result = await Service(_context)
            .PromoteToTaskAsync(_noteId, _featureId, "task contract body", "Promoted thought");

        var task = await _context.PlanningArtifacts.AsNoTracking()
            .SingleAsync(row => row.Id == result.TargetId);
        task.Kind.ShouldBe(PlanningArtifactKind.Task);
        task.FeatureId.ShouldBe(_featureId);
        task.Order.ShouldBe(0);
        task.Brief.ShouldBe("Promoted thought");

        result.TargetKind.ShouldBe(EntryLinkKind.Task);
        result.FeatureId.ShouldBe(_featureId);

        var edge = (await EdgesAsync(_context)).Single(row => row.Origin == EntryLinkOrigin.Manual);
        edge.ToKind.ShouldBe(EntryLinkKind.Task);
        edge.ToId.ShouldBe(task.Id);
        edge.ToId.ShouldNotBe(_featureId);
    }

    [Fact]
    public async Task PromoteToFeature_EdgeWriteFaults_LeavesNeitherCardNorEdge()
    {
        var before = await SnapshotNoteAsync(_context);

        await Should.ThrowAsync<InvalidOperationException>(
            () => Service(FaultedContext()).PromoteToFeatureAsync(
                _noteId, _boardProjectId, "Triage target", "body"));

        using var probe = _temp.CreateContext();
        (await probe.PlanningFeatures.AsNoTracking()
            .CountAsync(row => row.Title == "Triage target")).ShouldBe(0);
        (await EdgesAsync(probe)).ShouldBeEmpty();
        (await SnapshotNoteAsync(probe)).ShouldBe(before);
    }

    [Fact]
    public async Task PromoteToTask_EdgeWriteFaults_LeavesNeitherTaskNorEdge()
    {
        await Should.ThrowAsync<InvalidOperationException>(
            () => Service(FaultedContext()).PromoteToTaskAsync(_noteId, _featureId, "task contract body"));

        using var probe = _temp.CreateContext();
        (await probe.PlanningArtifacts.AsNoTracking().CountAsync()).ShouldBe(0);
        (await EdgesAsync(probe)).ShouldBeEmpty();
    }

    [Fact]
    public async Task ReSavingTheNoteAfterPromotion_KeepsThePromotionEdge_AndNeverDoubleBooksThePair()
    {
        var result = await Service(_context)
            .PromoteToFeatureAsync(_noteId, _boardProjectId, "Triage target", "body");

        await ReSaveNoteAsync($"Still the same thought, now citing [[feature:{result.FeatureSlug}]].");

        var pair = (await EdgesAsync(_context))
            .Where(row => row.ToKind == EntryLinkKind.Feature && row.ToId == result.TargetId)
            .ToList();

        pair.ShouldHaveSingleItem();
        pair[0].Origin.ShouldBe(EntryLinkOrigin.Manual);
    }

    [Fact]
    public async Task RepeatPromotion_RefusedWithTheNamedError_AndNothingSecondIsCreated()
    {
        var first = await Service(_context)
            .PromoteToFeatureAsync(_noteId, _boardProjectId, "Triage target", "body");

        var ex = await Should.ThrowAsync<PlanningException>(() => Service(_context)
            .PromoteToFeatureAsync(_noteId, _boardProjectId, "Triage target again", "second body"));

        ex.Code.ShouldBe(PlanningFailureCode.TriageAlreadyPromoted);
        ex.Message.ShouldContain(first.TargetId.ToString());
        ex.Message.ShouldContain(first.FeatureSlug, Case.Insensitive);

        var features = await _context.PlanningFeatures.AsNoTracking()
            .Where(row => row.ProjectId == _boardProjectId && row.Title != "Target Feature")
            .CountAsync();
        features.ShouldBe(1);
        (await EdgesAsync(_context)).Count(row => row.Origin == EntryLinkOrigin.Manual).ShouldBe(1);
    }

    [Fact]
    public async Task RepeatTaskPromotion_RefusedWithTheNamedError()
    {
        await Service(_context).PromoteToTaskAsync(_noteId, _featureId, "task contract body");

        var ex = await Should.ThrowAsync<PlanningException>(() => Service(_context)
            .PromoteToTaskAsync(_noteId, _featureId, "second task body"));

        ex.Code.ShouldBe(PlanningFailureCode.TriageAlreadyPromoted);
        ex.Message.ShouldContain("077-target");
        (await _context.PlanningArtifacts.AsNoTracking().CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task PromotingAgain_AfterThePromotedCardWasDeleted_Succeeds()
    {
        var first = await Service(_context)
            .PromoteToFeatureAsync(_noteId, _boardProjectId, "Triage target", "body");

        // The guard follows the triage hygiene: an edge whose target row is gone links nothing, so the note is
        // back in the pile and a second promotion is a fresh act, not a duplicate.
        _context.PlanningFeatures.Remove(
            await _context.PlanningFeatures.SingleAsync(row => row.Id == first.TargetId));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var second = await Service(_context)
            .PromoteToFeatureAsync(_noteId, _boardProjectId, "Triage target", "body");

        second.TargetId.ShouldNotBe(first.TargetId);
        (await EdgesAsync(_context)).Count(row => row.Origin == EntryLinkOrigin.Manual).ShouldBe(2);
    }

    [Fact]
    public async Task PromotingABuriedNote_Refused_AndNothingIsCreated()
    {
        var note = await _context.ContextEntries.SingleAsync(row => row.Id == _noteId);
        note.Lift("replaced", "ui", null, null);
        await _context.SaveChangesAsync();

        var ex = await Should.ThrowAsync<PlanningException>(() => Service(_context)
            .PromoteToFeatureAsync(_noteId, _boardProjectId, "Triage target", "body"));

        ex.Code.ShouldBe(PlanningFailureCode.TriageNoteNotPromotable);
        (await _context.PlanningFeatures.AsNoTracking()
            .CountAsync(row => row.Title == "Triage target")).ShouldBe(0);
    }

    [Fact]
    public async Task PromotingAnUnknownEntry_Refused_AndNothingIsCreated()
    {
        var ex = await Should.ThrowAsync<PlanningException>(() => Service(_context)
            .PromoteToFeatureAsync(Guid.NewGuid(), _boardProjectId, "Triage target", "body"));

        ex.Code.ShouldBe(PlanningFailureCode.MissingEntry);
        (await _context.PlanningFeatures.AsNoTracking()
            .CountAsync(row => row.Title == "Triage target")).ShouldBe(0);
    }

    [Fact]
    public async Task PromotingToAnUnknownFeature_Refused_AndNoEdgeIsWritten()
    {
        var ex = await Should.ThrowAsync<PlanningException>(() => Service(_context)
            .PromoteToTaskAsync(_noteId, Guid.NewGuid(), "task contract body"));

        ex.Code.ShouldBe(PlanningFailureCode.MissingFeature);
        (await EdgesAsync(_context)).ShouldBeEmpty();
    }

    [Fact]
    public async Task ATaskPromotion_MakesTheNoteLinked_NotAnOrphan_ThroughTheTaskArm()
    {
        var triage = new EntryLinkTriage(_context);
        (await triage.WhereUnlinked(Notes()).AnyAsync(row => row.Id == _noteId)).ShouldBeTrue(
            "the note is in the pile before the act, or triage has nothing to exit");

        await Service(_context).PromoteToTaskAsync(_noteId, _featureId, "task contract body");
        _context.ChangeTracker.Clear();

        (await triage.WhereLinked(Notes()).Select(row => row.Id).ToListAsync()).ShouldContain(_noteId);
        (await triage.WhereUnlinked(Notes()).Select(row => row.Id).ToListAsync()).ShouldNotContain(_noteId);
    }

    [Fact]
    public async Task AFeaturePromotion_MakesTheNoteLinked_NotAnOrphan()
    {
        await Service(_context).PromoteToFeatureAsync(_noteId, _boardProjectId, "Triage target", "body");
        _context.ChangeTracker.Clear();

        var triage = new EntryLinkTriage(_context);
        (await triage.WhereLinked(Notes()).Select(row => row.Id).ToListAsync()).ShouldContain(_noteId);
        (await triage.WhereUnlinked(Notes()).Select(row => row.Id).ToListAsync()).ShouldNotContain(_noteId);
    }

    // The state the handoff described as the ordinary triage act: the note already names the very feature it is
    // being sorted into. Two doors, one fresh artifact id, so the promotion must not trip the unique index — and
    // after it the note is linked by its manual edge while the mention stays where it was written.
    [Fact]
    public async Task PromoteToTask_NoteAlreadyNamingThatFeature_PromotesWithoutThrowing()
    {
        await SeedDerivedFeatureEdgeAsync(_featureId);

        var result = await Service(_context).PromoteToTaskAsync(_noteId, _featureId, "task contract body");

        var rows = await EdgesAsync(_context);
        var mention = rows.Single(row => row.ToKind == EntryLinkKind.Feature);
        mention.Origin.ShouldBe(EntryLinkOrigin.Derived);
        mention.ToId.ShouldBe(_featureId);

        var promotion = rows.Single(row => row.Origin == EntryLinkOrigin.Manual);
        promotion.ToKind.ShouldBe(EntryLinkKind.Task);
        promotion.ToId.ShouldBe(result.TargetId);

        _context.ChangeTracker.Clear();
        var triage = new EntryLinkTriage(_context);
        (await triage.WhereLinked(Notes()).Select(row => row.Id).ToListAsync()).ShouldContain(_noteId);
    }

    [Fact]
    public async Task PromotionEdgeOnAPairTheNoteAlreadyCarriesDerived_SupplantsTheDerivedRow()
    {
        await SeedDerivedFeatureEdgeAsync(_featureId);
        var service = Service(_context);

        await service.WriteProvenanceEdgeAsync(
            _noteId,
            new TriagePromotionService.Promotion(
                EntryLinkKind.Feature, _featureId, _featureId, "077-target", _boardProjectId));
        await _context.SaveChangesAsync();

        var rows = (await EdgesAsync(_context))
            .Where(row => row.ToKind == EntryLinkKind.Feature && row.ToId == _featureId)
            .ToList();

        rows.ShouldHaveSingleItem();
        rows[0].Origin.ShouldBe(EntryLinkOrigin.Manual);
    }

    [Fact]
    public async Task SupplantingTheDerivedRow_LivesInTheCallersTransaction_RollbackRestoresIt()
    {
        await SeedDerivedFeatureEdgeAsync(_featureId);

        await using var tx = await _context.Database.BeginTransactionAsync();
        await Service(_context).WriteProvenanceEdgeAsync(
            _noteId,
            new TriagePromotionService.Promotion(
                EntryLinkKind.Feature, _featureId, _featureId, "077-target", _boardProjectId));
        await _context.SaveChangesAsync();
        await tx.RollbackAsync();
        _context.ChangeTracker.Clear();

        var rows = (await EdgesAsync(_context))
            .Where(row => row.ToKind == EntryLinkKind.Feature && row.ToId == _featureId)
            .ToList();

        rows.ShouldHaveSingleItem();
        rows[0].Origin.ShouldBe(EntryLinkOrigin.Derived);
    }

    [Fact]
    public async Task TheSamePairWithoutSupplanting_ViolatesTheUniqueIndex_WhichIsWhyTheGuardExists()
    {
        await SeedDerivedFeatureEdgeAsync(_featureId);

        _context.EntryLinks.Add(EntryLink.CreateManual(_noteId, EntryLinkKind.Feature, _featureId));

        var ex = await Should.ThrowAsync<DbUpdateException>(() => _context.SaveChangesAsync());
        PlanningServiceGuard.IsUniqueViolation(ex).ShouldBeTrue();
    }

    private IQueryable<ContextEntry> Notes() =>
        _context.ContextEntries.Where(row =>
            row.ProjectId == _journalProjectId && row.Type == EntryType.Journal);

    private TriagePromotionService Service(OvermindDbContext context)
    {
        var dispatcher = TestPlanningDispatcher.Create(context, _materializer);
        return new TriagePromotionService(
            context,
            new EntryLinkTriage(context),
            new PlanningFeatureService(
                context,
                _materializer,
                new BacklogImportSeeder(context, new PlanningPolicy(context, new AppSettingsService(context))),
                new TaskDependencyService(context, dispatcher),
                dispatcher,
                NullLogger<PlanningFeatureService>.Instance,
                _batch),
            new PlanningArtifactService(
                context,
                dispatcher,
                new TaskDependencyService(context, dispatcher),
                _batch),
            _batch,
            dispatcher);
    }

    private OvermindDbContext FaultedContext() => _temp.CreateContext(new EdgeWriteFaultInterceptor());

    private async Task SeedDerivedFeatureEdgeAsync(Guid featureId)
    {
        await ReSaveNoteAsync("Worth board work, spun off [[feature:077-target]].");
        var derived = await _context.EntryLinks
            .SingleAsync(row => row.ToKind == EntryLinkKind.Feature && row.ToId == featureId);
        derived.Origin.ShouldBe(EntryLinkOrigin.Derived);
    }

    private async Task ReSaveNoteAsync(string content)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();
        var note = await _context.ContextEntries.SingleAsync(row => row.Id == _noteId);
        note.UpdateContent(content);
        await new EntryLinkWriter(
                _context, new EntryLinkResolver(_context, new JournalProjectStore(_context)))
            .ReplaceDerivedAsync(_noteId, WikiLinkParser.Parse(content), tx);
        await tx.CommitAsync();
        _context.ChangeTracker.Clear();
    }

    private Task<List<EntryLink>> EdgesAsync(OvermindDbContext context) =>
        context.EntryLinks.AsNoTracking().OrderBy(row => row.CreatedAt).ToListAsync();

    private async Task<NoteSnapshot> SnapshotNoteAsync(OvermindDbContext context)
    {
        var note = await context.ContextEntries.AsNoTracking().SingleAsync(row => row.Id == _noteId);
        return new NoteSnapshot(
            note.Title,
            note.Content,
            note.State,
            note.UpdatedAt,
            note.CurrentVersion,
            await context.EntryTags.AsNoTracking()
                .CountAsync(row => row.EntryId == _noteId),
            await context.EntryVersions.AsNoTracking()
                .CountAsync(row => row.EntryId == _noteId),
            await context.ContextEntries.AsNoTracking()
                .CountAsync(row => row.ProjectId == _journalProjectId));
    }

    private sealed record NoteSnapshot(
        string Title,
        string Content,
        string State,
        DateTime UpdatedAt,
        int CurrentVersion,
        int TagRows,
        int VersionRows,
        int JournalRows);

    // Faults exactly where the promotion is vulnerable: the artifact half has been flushed inside the
    // transaction, the provenance edge is the change still pending. Any other fault would prove less.
    private sealed class EdgeWriteFaultInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData, InterceptionResult<int> result)
        {
            FailOnEdgeWrite(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            FailOnEdgeWrite(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private static void FailOnEdgeWrite(DbContext? context)
        {
            if (context is not null && context.ChangeTracker.Entries<EntryLink>()
                    .Any(entry => entry.State == EntityState.Added))
            {
                throw new InvalidOperationException(
                    "Forced fault between the artifact flush and the provenance edge flush.");
            }
        }
    }
}
