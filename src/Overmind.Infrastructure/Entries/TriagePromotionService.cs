using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;

namespace Overmind.Infrastructure.Entries;

// The exit door of the orphan pile: a note becomes board work, and the manual edge this writes is the only record
// that it did. No state column, no tag — triage reads the edge, which is why both halves must commit together.
// A feature the note cannot reach would strand the thought on the board; an edge without its artifact would hide
// a note that is not sorted anywhere.
//
// The creation halves are not restated here — the planning services own the slug, the number guard, the queue
// tail and the task skeleton. The promotion drives them in the batch mode PlanningBatchService already uses:
// this door owns the transaction, they stage, and the flush is its own.
public sealed class TriagePromotionService
{
    private readonly OvermindDbContext _db;
    private readonly PlanningFeatureService _features;
    private readonly PlanningArtifactService _artifacts;
    private readonly PlanningBatchScope _batch;
    private readonly PlanningMaterializationDispatcher _materialization;

    public TriagePromotionService(
        OvermindDbContext db,
        PlanningFeatureService features,
        PlanningArtifactService artifacts,
        PlanningBatchScope batch,
        PlanningMaterializationDispatcher materialization)
    {
        _db = db;
        _features = features;
        _artifacts = artifacts;
        _batch = batch;
        _materialization = materialization;
    }

    public Task<TriagePromotionResult> PromoteToFeatureAsync(
        Guid entryId, Guid projectId, string title, string body,
        FeaturePriority priority = FeaturePriority.P2, CancellationToken ct = default) =>
        PromoteAsync(entryId, ct, async token =>
        {
            var feature = await _features.CreateAsync(projectId, null, title, body, priority, token);
            return new Promotion(
                EntryLinkKind.Feature, feature.Id, feature.Id, feature.Slug, feature.ProjectId);
        });

    public async Task<TriagePromotionResult> PromoteToTaskAsync(
        Guid entryId, Guid featureId, string body, string? brief = null, CancellationToken ct = default)
    {
        var feature = await _db.PlanningFeatures.AsNoTracking()
            .Where(row => row.Id == featureId)
            .Select(row => new { row.ProjectId, row.Slug })
            .FirstOrDefaultAsync(ct)
            ?? throw PlanningException.MissingFeature(featureId.ToString());

        return await PromoteAsync(entryId, ct, async token =>
        {
            var batch = await _artifacts.UpsertTasksAsync(
                featureId, [new PlanningTaskUpsert(body, Brief: brief)], token);

            return new Promotion(
                EntryLinkKind.Task, batch.Artifacts[0].Id, featureId, feature.Slug, feature.ProjectId);
        });
    }

    private async Task<TriagePromotionResult> PromoteAsync(
        Guid entryId, CancellationToken ct, Func<CancellationToken, Task<Promotion>> createAsync)
    {
        if (_db.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                $"Triage promotion of entry {entryId} owns its transaction and refuses to join another.");
        }

        await RequirePromotableNoteAsync(entryId, ct);
        await RequireNotPromotedAsync(entryId, ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        _batch.Active = true;

        Promotion promotion;
        try
        {
            promotion = await createAsync(ct);
            await FlushArtifactAsync(promotion, ct);
            await WriteProvenanceEdgeAsync(entryId, promotion, ct);
            await FlushEdgeAsync(entryId, promotion, ct);

            await tx.CommitAsync(ct);
        }
        finally
        {
            _batch.Active = false;
        }

        await _materialization.DispatchAsync(promotion.ProjectId, ct);

        return new TriagePromotionResult(
            entryId, promotion.Kind, promotion.TargetId, promotion.FeatureId, promotion.FeatureSlug);
    }

    private async Task RequirePromotableNoteAsync(Guid entryId, CancellationToken ct)
    {
        var state = await _db.ContextEntries.AsNoTracking()
            .Where(entry => entry.Id == entryId)
            .Select(entry => entry.State)
            .FirstOrDefaultAsync(ct);

        if (state is null)
        {
            throw PlanningException.MissingEntry(entryId);
        }

        if (state != ContextEntry.ActiveState)
        {
            throw PlanningException.TriageNoteNotPromotable(entryId, state);
        }
    }

    // Refused, not idempotent: the promotion creates a card the owner has to see, so silently returning the
    // first one's target would let a double click look like a sorted note while a second thought went unsaid.
    // Only an edge whose artifact still exists counts as a promotion — delete the card and the note is back in
    // the pile to be promoted again, which is the same hygiene the triage predicate applies to buried rows.
    private async Task RequireNotPromotedAsync(Guid entryId, CancellationToken ct)
    {
        var prior = await _db.EntryLinks.AsNoTracking()
            .Where(link => link.FromEntryId == entryId
                           && link.Origin == EntryLinkOrigin.Manual
                           && ((link.ToKind == EntryLinkKind.Feature
                                    && _db.PlanningFeatures.Any(feature => feature.Id == link.ToId))
                                || (link.ToKind == EntryLinkKind.Task
                                    && _db.PlanningArtifacts.Any(artifact => artifact.Id == link.ToId))))
            .Select(link => new { link.ToKind, link.ToId })
            .FirstOrDefaultAsync(ct);

        if (prior is not null)
        {
            throw PlanningException.TriageAlreadyPromoted(
                entryId, prior.ToKind, prior.ToId, await DescribeTargetAsync(prior.ToKind, prior.ToId, ct));
        }
    }

    // The refusal has to be actionable from the inbox chip alone, so it names the card the note already became.
    private async Task<string> DescribeTargetAsync(EntryLinkKind kind, Guid targetId, CancellationToken ct)
    {
        if (kind == EntryLinkKind.Feature)
        {
            var slug = await _db.PlanningFeatures.AsNoTracking()
                .Where(feature => feature.Id == targetId)
                .Select(feature => feature.Slug)
                .FirstOrDefaultAsync(ct);

            return slug is null ? "a card that is no longer on the board" : $"'{slug}'";
        }

        var task = await _db.PlanningArtifacts.AsNoTracking()
            .Where(artifact => artifact.Id == targetId)
            .Select(artifact => new { artifact.Order, artifact.FeatureId })
            .FirstOrDefaultAsync(ct);

        if (task is null)
        {
            return "a task that is no longer on the board";
        }

        var parent = await _db.PlanningFeatures.AsNoTracking()
            .Where(feature => feature.Id == task.FeatureId)
            .Select(feature => feature.Slug)
            .FirstOrDefaultAsync(ct);

        return $"'{parent}' task {task.Order:00}";
    }

    private async Task FlushArtifactAsync(Promotion promotion, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (PlanningServiceGuard.IsUniqueViolation(ex))
        {
            throw promotion.Kind == EntryLinkKind.Feature
                ? PlanningException.DuplicateSlug(promotion.FeatureSlug)
                : PlanningException.TaskBatchConcurrentWrite();
        }
    }

    // Internal so the collision branch is testable on its own: through v1's two doors the target id is always
    // freshly created, so no derived row can sit on the key yet — see the task log.
    internal async Task WriteProvenanceEdgeAsync(Guid entryId, Promotion promotion, CancellationToken ct = default)
    {
        // The unique index on (FromEntryId, ToKind, ToId) allows one row per directed pair, and a note that
        // already names its own destination carries a derived row on the very key the promotion is about to
        // claim. EntryLinkWriter suppresses derived-over-manual only, so supplanting here is what keeps the most
        // ordinary triage act from throwing: the manual edge is the one the triage state derives from.
        await _db.EntryLinks
            .Where(link => link.FromEntryId == entryId
                           && link.Origin == EntryLinkOrigin.Derived
                           && link.ToKind == promotion.Kind
                           && link.ToId == promotion.TargetId)
            .ExecuteDeleteAsync(ct);

        _db.EntryLinks.Add(EntryLink.CreateManual(entryId, promotion.Kind, promotion.TargetId));
    }

    private async Task FlushEdgeAsync(Guid entryId, Promotion promotion, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (PlanningServiceGuard.IsUniqueViolation(ex))
        {
            throw PlanningException.TriageAlreadyPromoted(entryId, promotion.Kind, promotion.TargetId);
        }
    }

    internal sealed record Promotion(
        EntryLinkKind Kind,
        Guid TargetId,
        Guid FeatureId,
        string FeatureSlug,
        Guid ProjectId);
}
