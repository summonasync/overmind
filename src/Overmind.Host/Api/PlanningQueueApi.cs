using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Mapping;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public static class PlanningQueueApi
{
    internal static async Task<IResult> QueueRead(
        Guid projectId, int? limit, OvermindDbContext db, FeatureClaimService claims,
        DirectiveService directives, CancellationToken ct)
    {
        if (limit is < 1)
        {
            return InvalidField("Limit", "limit must be a positive int; omit it for the full queue.");
        }

        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            return HttpErrors.NotFound($"Project '{projectId}' was not found.");
        }

        var features = await LoadProjectFeatures(db, projectId, ct);
        var graph = features.ToDictionary(f => f.Id);
        var queue = features
            .Where(f => f.Status != PlanningFeatureStatus.Done)
            .OrderBy(f => f.Priority)
            .ThenBy(f => f.QueuePosition)
            .ThenBy(f => f.Slug)
            .ToList();

        var claimLiveness = await claims.ClaimLivenessAsync(projectId, features, ct);
        var laneHolders = PlanningMapper.LaneHoldersByLane(features, claimLiveness);
        var overlapIndex = await PlanningFeaturesApi.OverlapIndexAsync(db, projectId, ct);
        var directiveCounts = await directives.OpenCountsAsync(projectId, ct);
        return Results.Ok(new PlanningQueueResponse(queue
            .Take(limit ?? queue.Count)
            .Select(f => PlanningMapper.ToQueueItem(
                f, graph, claimLiveness.GetValueOrDefault(f.Id),
                laneHolders.GetValueOrDefault(f.Lane ?? string.Empty),
                overlapIndex.GetValueOrDefault(f.Id),
                directiveCounts.GetValueOrDefault(f.Id)))
            .ToArray()));
    }

    internal static async Task<IResult> QueueMove(
        Guid projectId,
        string slug,
        PlanningQueueMoveRequest request,
        IValidator<PlanningQueueMoveRequest> validator,
        FeatureQueueService queue,
        FeatureClaimService claims,
        DirectiveService directives,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        try
        {
            await (request.Move!.Trim().ToLowerInvariant() switch
            {
                "up" => queue.MoveUpAsync(projectId, slug, ct),
                "down" => queue.MoveDownAsync(projectId, slug, ct),
                "first" => queue.MoveFirstAsync(projectId, slug, ct),
                "last" => queue.MoveLastAsync(projectId, slug, ct),
                "toposition" => queue.MoveToPositionAsync(projectId, slug, request.Position!.Value, ct),
                _ => throw new InvalidOperationException($"Unknown queue move '{request.Move}'."),
            });
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }

        var features = await LoadProjectFeatures(db, projectId, ct);
        var moved = features.FirstOrDefault(f => f.Slug == slug);
        if (moved is null)
        {
            return HttpErrors.NotFound($"Feature '{slug}' was not found.");
        }

        var claimLiveness = await claims.ClaimLivenessAsync(projectId, features, ct);
        var laneHolders = PlanningMapper.LaneHoldersByLane(features, claimLiveness);
        var overlapIndex = await PlanningFeaturesApi.OverlapIndexAsync(db, projectId, ct);
        var directiveCounts = await directives.OpenCountsAsync(projectId, ct);
        return Results.Ok(PlanningMapper.ToQueueItem(
            moved, features.ToDictionary(f => f.Id), claimLiveness.GetValueOrDefault(moved.Id),
            laneHolders.GetValueOrDefault(moved.Lane ?? string.Empty),
            overlapIndex.GetValueOrDefault(moved.Id),
            directiveCounts.GetValueOrDefault(moved.Id)));
    }

    internal static async Task<IResult> Reorder(
        Guid projectId,
        PlanningQueueReorderRequest request,
        IValidator<PlanningQueueReorderRequest> validator,
        FeatureQueueService queue,
        FeatureClaimService claims,
        DirectiveService directives,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        if (!PlanningFeaturesApi.TryParseEnum(request.Priority, out FeaturePriority priority))
        {
            return InvalidField(
                nameof(request.Priority),
                $"Must be one of: {string.Join(", ", Enum.GetNames<FeaturePriority>().Select(n => n.ToLowerInvariant()))}.");
        }

        try
        {
            await queue.ReorderBandAsync(projectId, priority, request.Slugs, ct);
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }

        var features = await LoadProjectFeatures(db, projectId, ct);
        var graph = features.ToDictionary(f => f.Id);
        var claimLiveness = await claims.ClaimLivenessAsync(projectId, features, ct);
        var laneHolders = PlanningMapper.LaneHoldersByLane(features, claimLiveness);
        var overlapIndex = await PlanningFeaturesApi.OverlapIndexAsync(db, projectId, ct);
        var directiveCounts = await directives.OpenCountsAsync(projectId, ct);
        return Results.Ok(new PlanningQueueResponse(features
            .Where(f => f.Priority == priority && f.Status != PlanningFeatureStatus.Done)
            .OrderBy(f => f.QueuePosition)
            .Select(f => PlanningMapper.ToQueueItem(
                f, graph, claimLiveness.GetValueOrDefault(f.Id),
                laneHolders.GetValueOrDefault(f.Lane ?? string.Empty),
                overlapIndex.GetValueOrDefault(f.Id),
                directiveCounts.GetValueOrDefault(f.Id)))
            .ToArray()));
    }

    private static async Task<List<PlanningFeature>> LoadProjectFeatures(
        OvermindDbContext db, Guid projectId, CancellationToken ct) =>
        await db.PlanningFeatures
            .Include(f => f.Artifacts)
            .Include(f => f.BlockedBy)
            .Where(f => f.ProjectId == projectId)
            .ToListAsync(ct);

    private static IResult InvalidField(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
