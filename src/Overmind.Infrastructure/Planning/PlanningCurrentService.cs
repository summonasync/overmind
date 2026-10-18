using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class PlanningCurrentService
{
    private readonly OvermindDbContext _db;
    private readonly PlanningPolicy _policy;
    private readonly FeatureDependencyService _dependencies;
    private readonly TaskDependencyService _taskDependencies;
    private readonly DirectiveService _directives;

    public PlanningCurrentService(
        OvermindDbContext db,
        PlanningPolicy policy,
        FeatureDependencyService dependencies,
        TaskDependencyService taskDependencies,
        DirectiveService directives)
    {
        _db = db;
        _policy = policy;
        _dependencies = dependencies;
        _taskDependencies = taskDependencies;
        _directives = directives;
    }

    public async Task<PlanningCurrentResult> GetCurrentAsync(
        Guid projectId, string slug, CancellationToken ct = default)
    {
        var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            throw PlanningException.MissingProject(projectId);
        }

        var feature = await _db.PlanningFeatures
            .Include(f => f.Artifacts)
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Slug == slug, ct)
            ?? throw PlanningException.MissingFeature(slug);

        var currentFeature = new PlanningCurrentFeatureView(
            feature.Slug, feature.Title, feature.Status,
            await _directives.ListOpenAsync(feature.Id, ct));

        var openBlockers = await _dependencies.OpenBlockersAsync(projectId, slug, ct);
        if (openBlockers.Count > 0)
        {
            return new PlanningCurrentResult(
                "stop", currentFeature, null,
                $"STOP: feature '{feature.Slug}' is blocked by unclosed features: {BlockerList(openBlockers)}. " +
                "Reads stay open; task issuance stops until every blocker closes (done or review).");
        }

        var gated = await _policy.ResolveValidationGateAsync(projectId, ct);

        var spec = feature.Artifacts.FirstOrDefault(a => a.Kind == PlanningArtifactKind.Spec);
        var plan = feature.Artifacts.FirstOrDefault(a => a.Kind == PlanningArtifactKind.Plan);

        if (gated && (spec is null || spec.Status == PlanningArtifactStatus.Draft))
        {
            return new PlanningCurrentResult(
                "stop", currentFeature, null,
                $"STOP: spec awaits human validation — it cannot be approved through MCP. " +
                $"Send overmind_notify(kind=approval, featureSlug={feature.Slug}) and wait for the human's reply — validation is never agent-approvable.");
        }

        if (gated && spec is { Status: PlanningArtifactStatus.Validated } &&
            (plan is null || plan.Status == PlanningArtifactStatus.Draft))
        {
            return new PlanningCurrentResult(
                "stop", currentFeature, null,
                $"STOP: plan awaits human validation — it cannot be approved through MCP. " +
                $"Send overmind_notify(kind=approval, featureSlug={feature.Slug}) and wait for the human's reply — validation is never agent-approvable.");
        }

        var tasks = feature.Artifacts
            .Where(a => a.Kind == PlanningArtifactKind.Task)
            .OrderBy(a => a.Order)
            .ToList();

        if (tasks.Count == 0)
        {
            return new PlanningCurrentResult(
                "empty", currentFeature, null,
                "No tasks yet — decompose the validated plan via planning_upsert_tasks: each task an execution contract (goal, scope, anti-scope, acceptance-criteria, refs).");
        }

        if (tasks.All(t => t.Status == PlanningArtifactStatus.Done))
        {
            return new PlanningCurrentResult(
                "empty", currentFeature, null, ResultStateReason(feature));
        }

        var candidates = tasks
            .Where(t => t.Status is PlanningArtifactStatus.Todo or PlanningArtifactStatus.InProgress)
            .ToList();

        if (candidates.Count > 0)
        {
            var blockedStates = await _taskDependencies.BlockedOrdersAsync(feature.Id, ct);
            var blockedOrders = blockedStates
                .Where(s => s.Blocked)
                .Select(s => s.Order)
                .ToHashSet();

            var actionable = candidates.FirstOrDefault(t => !blockedOrders.Contains(t.Order));
            if (actionable is not null)
            {
                return new PlanningCurrentResult(
                    "task",
                    currentFeature,
                    actionable,
                    null,
                    SpecPlanWarnings(feature),
                    blockedStates.ToDictionary(s => s.Order));
            }

            return new PlanningCurrentResult(
                "stop",
                currentFeature,
                null,
                $"STOP: every actionable task is blocked by unclosed tasks: " +
                $"{UnclosedChain(candidates, await _taskDependencies.UnclosedBlockerOrdersAsync(feature.Id, ct))}. " +
                "Reads stay open; task issuance stops until every blocker closes (done or review).");
        }

        var inReview = tasks.Count(t => t.Status == PlanningArtifactStatus.Review);
        if (inReview > 0)
        {
            return new PlanningCurrentResult(
                "held", currentFeature, null,
                $"{inReview} in review — finish the reviews, then close each task with planning_set_status.");
        }

        var blocked = tasks.First(t => t.Status == PlanningArtifactStatus.Blocked);
        return new PlanningCurrentResult(
            "task",
            currentFeature,
            blocked,
            "The next task is blocked.",
            SpecPlanWarnings(feature));
    }

    private static string UnclosedChain(
        IReadOnlyList<PlanningArtifact> candidates,
        IReadOnlyDictionary<int, IReadOnlyList<int>> unclosedBlockerOrders)
    {
        var links = new List<string>(candidates.Count);
        foreach (var candidate in candidates)
        {
            links.Add($"task {candidate.Order} blocked by [{string.Join(", ", unclosedBlockerOrders.GetValueOrDefault(candidate.Order, []))}]");
        }

        return string.Join(", ", links);
    }

    private static string ResultStateReason(PlanningFeature feature)
    {
        var result = feature.Artifacts.FirstOrDefault(a => a.Kind == PlanningArtifactKind.Result);
        return result?.Status switch
        {
            PlanningArtifactStatus.Draft => $"Result artifact drafted — validate it via the proof flow: " +
                $"send overmind_notify(kind=approval, featureSlug={feature.Slug}) and wait for the human's reply.",
            PlanningArtifactStatus.Validated => "Result validated — closing the feature needs the human's done tap on the board.",
            _ => "All tasks are done — write the result artifact via planning_upsert_artifact(kind=result)."
        };
    }

    private static string[]? SpecPlanWarnings(PlanningFeature feature)
    {
        var spec = feature.Artifacts.FirstOrDefault(a => a.Kind == PlanningArtifactKind.Spec);
        var plan = feature.Artifacts.FirstOrDefault(a => a.Kind == PlanningArtifactKind.Plan);

        var warnings = new List<string>(2);
        if (spec is null)
        {
            warnings.Add("spec missing");
        }
        else if (spec.Status == PlanningArtifactStatus.Draft)
        {
            warnings.Add("spec not validated");
        }

        if (plan is null || plan.Status == PlanningArtifactStatus.Draft)
        {
            warnings.Add("plan not validated");
        }

        return warnings.Count == 0 ? null : [.. warnings];
    }

    private static string BlockerList(IReadOnlyList<FeatureDependencyEntry> openBlockers) =>
        string.Join(", ", openBlockers.Select(FormatBlocker));

    private static string FormatBlocker(FeatureDependencyEntry entry) =>
        $"{entry.Slug} ({entry.Status.ToString().ToLowerInvariant()})";
}
