using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Mapping;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public static class PlanningFeaturesApi
{
    public static RouteGroupBuilder MapPlanningFeaturesApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");

        group.MapGet("/projects/{projectId:guid}/planning/features", Board);
        group.MapPost("/projects/{projectId:guid}/planning/features", CreateFeature);
        group.MapGet("/projects/{projectId:guid}/planning/features/{slug}", GetFeature);
        group.MapPut("/projects/{projectId:guid}/planning/features/{slug}", UpdateFeature);
        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/claim", Claim);
        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/unclaim", Unclaim);
        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/pin-code", PlanningCodePinApi.PinCode);
        group.MapDelete("/projects/{projectId:guid}/planning/features/{slug}", DeleteFeature);
        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/queue-move", PlanningQueueApi.QueueMove);
        group.MapPost("/projects/{projectId:guid}/planning/queue/reorder", PlanningQueueApi.Reorder);

        return group;
    }

    internal static async Task<IResult> Board(
        Guid projectId,
        string? status,
        string? priority,
        string? lane,
        string? claimedByAlias,
        bool? unclaimedOnly,
        OvermindDbContext db, FeatureClaimService claims, DirectiveService directives,
        CancellationToken ct)
    {
        PlanningFeatureStatus? statusFilter = null;
        if (status is not null)
        {
            if (!TryParseEnum(status, out PlanningFeatureStatus parsedStatus))
            {
                return InvalidValue(nameof(status), Enum.GetNames<PlanningFeatureStatus>());
            }

            statusFilter = parsedStatus;
        }

        FeaturePriority? priorityFilter = null;
        if (priority is not null)
        {
            if (!TryParseEnum(priority, out FeaturePriority parsedPriority))
            {
                return InvalidValue(nameof(priority), Enum.GetNames<FeaturePriority>());
            }

            priorityFilter = parsedPriority;
        }

        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            return HttpErrors.NotFound($"Project '{projectId}' was not found.");
        }

        var query = db.PlanningFeatures
            .Include(f => f.Artifacts)
            .Include(f => f.BlockedBy)
            .Where(f => f.ProjectId == projectId);
        if (statusFilter is not null)
        {
            query = query.Where(f => f.Status == statusFilter);
        }

        if (priorityFilter is not null)
        {
            query = query.Where(f => f.Priority == priorityFilter);
        }

        if (!string.IsNullOrWhiteSpace(lane))
        {
            var laneName = lane.Trim();
            query = query.Where(f => f.Lane != null && f.Lane.ToLower() == laneName.ToLower());
        }

        if (!string.IsNullOrWhiteSpace(claimedByAlias))
        {
            var alias = claimedByAlias.Trim();
            query = query.Where(f => f.ClaimedByAlias == alias);
        }

        if (unclaimedOnly == true)
        {
            query = query.Where(f => f.ClaimSessionId == null);
        }

        var features = await query
            .OrderBy(f => f.Slug)
            .ToListAsync(ct);

        var projectFeatures = features.ToDictionary(f => f.Id);
        var claimLiveness = await claims.ClaimLivenessAsync(projectId, features, ct);
        var laneHolders = PlanningMapper.LaneHoldersByLane(features, claimLiveness);
        var overlapIndex = await OverlapIndexAsync(db, projectId, ct);
        var directiveCounts = await directives.OpenCountsAsync(projectId, ct);
        return Results.Ok(features
            .Select(f => PlanningMapper.ToBoardItem(
                f, PlanningMapper.HasOpenBlocker(f, projectFeatures), claimLiveness.GetValueOrDefault(f.Id),
                laneHolders.GetValueOrDefault(f.Lane ?? string.Empty),
                overlapIndex.GetValueOrDefault(f.Id),
                directiveCounts.GetValueOrDefault(f.Id)))
            .ToArray());
    }

    internal static async Task<IResult> CreateFeature(
        Guid projectId,
        CreatePlanningFeatureRequest request,
        IValidator<CreatePlanningFeatureRequest> validator,
        PlanningFeatureService service,
        FeatureDependencyService dependencies,
        TaskDependencyService taskDependencies,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var priority = FeaturePriority.P2;
        if (request.Priority is not null)
        {
            if (!TryParseEnum(request.Priority, out FeaturePriority parsed))
            {
                return InvalidValue(nameof(request.Priority), Enum.GetNames<FeaturePriority>());
            }

            priority = parsed;
        }

        PlanningFeature created;
        try
        {
            created = await service.CreateAsync(
                projectId, request.Slug, request.Title, request.Body ?? string.Empty, priority, ct);
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }

        var feature = await LoadFeatureWithArtifacts(db, projectId, created.Slug, ct);
        return Results.Created(
            $"/api/projects/{projectId}/planning/features/{created.Slug}",
            PlanningMapper.ToFeatureResponse(
                feature!, await dependencies.ListDependenciesAsync(projectId, created.Slug, ct),
                await BlockedStatesAsync(taskDependencies, feature!, ct)));
    }

    internal static async Task<IResult> GetFeature(
        Guid projectId,
        string slug,
        bool? includeArtifacts,
        string? sections,
        OvermindDbContext db,
        FeatureClaimService claims,
        FeatureDependencyService dependencies,
        TaskDependencyService taskDependencies,
        DirectiveService directives,
        CancellationToken ct)
    {
        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            return HttpErrors.NotFound($"Project '{projectId}' was not found.");
        }

        var feature = await LoadFeatureWithArtifacts(db, projectId, slug, ct);
        if (feature is null)
        {
            return HttpErrors.NotFound($"Feature '{slug}' was not found.");
        }

        var claimState = (await claims.ClaimLivenessAsync(projectId, [feature], ct))
            .GetValueOrDefault(feature.Id);
        var overlapIndex = await OverlapIndexAsync(db, projectId, ct);
        var openDirectives = await directives.ListOpenAsync(feature.Id, ct);
        var requested = AgentPlanningApi.RequestedSections(sections);
        var sliced = requested.Count > 0
            ? PlanningSections.FilteredRead(feature.Body, requested)
            : null;

        if (includeArtifacts == false)
        {
            var roster = PlanningMapper.ToFeatureRosterResponse(
                feature, await dependencies.ListDependenciesAsync(projectId, slug, ct),
                await BlockedStatesAsync(taskDependencies, feature, ct), claimState,
                overlaps: overlapIndex.GetValueOrDefault(feature.Id),
                openDirectiveCount: openDirectives.Count);
            return Results.Ok(Narrow(roster, sliced));
        }

        var response = PlanningMapper.ToFeatureResponse(
            feature, await dependencies.ListDependenciesAsync(projectId, slug, ct),
            await BlockedStatesAsync(taskDependencies, feature, ct), claimState,
            overlaps: overlapIndex.GetValueOrDefault(feature.Id),
            openDirectives: openDirectives);
        return Results.Ok(Narrow(response, sliced));
    }

    private static PlanningFeatureRosterResponse Narrow(
        PlanningFeatureRosterResponse response, PlanningFilteredRead? sliced) =>
        sliced is null
            ? response
            : response with { Body = sliced.Body, MissingSections = [.. sliced.MissingNames] };

    private static PlanningFeatureResponse Narrow(
        PlanningFeatureResponse response, PlanningFilteredRead? sliced) =>
        sliced is null
            ? response
            : response with { Body = sliced.Body, MissingSections = [.. sliced.MissingNames] };

    internal static async Task<IResult> ListArtifacts(
        Guid projectId,
        string slug,
        string? kind,
        OvermindDbContext db,
        TaskDependencyService taskDependencies,
        CancellationToken ct)
    {
        PlanningArtifactKind? kindFilter = null;
        if (kind is not null)
        {
            if (!TryParseEnum(kind, out PlanningArtifactKind parsed))
            {
                return HttpErrors.BadRequest(
                    $"Unknown kind '{kind}'. Allowed values: spec, plan, task, result.");
            }

            kindFilter = parsed;
        }

        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            return HttpErrors.NotFound($"Project '{projectId}' was not found.");
        }

        var feature = await LoadFeatureWithArtifacts(db, projectId, slug, ct);
        if (feature is null)
        {
            return HttpErrors.NotFound($"Feature '{slug}' was not found.");
        }

        var blockedStates = await BlockedStatesAsync(taskDependencies, feature, ct);
        var artifacts = feature.Artifacts
            .Where(a => kindFilter is null || a.Kind == kindFilter)
            .OrderBy(a => a.Kind == PlanningArtifactKind.Task)
            .ThenBy(a => a.Kind)
            .ThenBy(a => a.Order)
            .Select(a => PlanningMapper.ToRosterItem(a, blockedStates))
            .ToArray();

        return Results.Ok(new PlanningArtifactRosterResponse(artifacts));
    }

    // Human door: no session header, no lock — the board claims and releases anything.
    internal static async Task<IResult> Claim(
        Guid projectId,
        string slug,
        ClaimPlanningFeatureRequest request,
        FeatureClaimService claims,
        CancellationToken ct)
    {
        try
        {
            var feature = await claims.ClaimAsync(
                projectId, slug, request.Alias, request.Vendor, null, request.OverrideLaneCheck, ct);
            return Results.Ok(await FeatureClaimEcho.ForAsync(claims, projectId, feature, request.OverrideLaneCheck, ct));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
        catch (ArgumentException ex)
        {
            return HttpErrors.BadRequest(ex.Message);
        }
    }

    internal static async Task<IResult> Unclaim(
        Guid projectId,
        string slug,
        FeatureClaimService claims,
        CancellationToken ct)
    {
        try
        {
            var feature = await claims.UnclaimAsync(projectId, slug, null, ct);
            return Results.Ok(await FeatureClaimEcho.ForAsync(claims, projectId, feature, overrideLaneCheck: false, ct));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    internal static async Task<IResult> UpdateFeature(
        Guid projectId,
        string slug,
        UpdatePlanningFeatureRequest request,
        IValidator<UpdatePlanningFeatureRequest> validator,
        PlanningFeatureService service,
        FeatureClaimService claims,
        FeatureDependencyService dependencies,
        TaskDependencyService taskDependencies,
        DirectiveService directives,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        FeaturePriority? priority = null;
        if (request.Priority is not null)
        {
            if (!TryParseEnum(request.Priority, out FeaturePriority parsed))
            {
                return InvalidValue(nameof(request.Priority), Enum.GetNames<FeaturePriority>());
            }

            priority = parsed;
        }

        try
        {
            await service.UpdateAsync(
                projectId, slug, request.Title, request.Body, request.Status, priority,
                request.Lane, request.CodeSurfaces, ct);
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }

        var feature = await LoadFeatureWithArtifacts(db, projectId, slug, ct);
        var overlapIndex = await OverlapIndexAsync(db, projectId, ct);
        return Results.Ok(PlanningMapper.ToFeatureResponse(
            feature!, await dependencies.ListDependenciesAsync(projectId, slug, ct),
            await BlockedStatesAsync(taskDependencies, feature!, ct),
            (await claims.ClaimLivenessAsync(projectId, [feature!], ct)).GetValueOrDefault(feature!.Id),
            overlaps: overlapIndex.GetValueOrDefault(feature!.Id),
            openDirectives: await directives.ListOpenAsync(feature!.Id, ct)));
    }

    internal static async Task<IResult> DeleteFeature(
        Guid projectId, string slug, PlanningFeatureService service, CancellationToken ct)
    {
        try
        {
            await service.DeleteAsync(projectId, slug, ct);
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }

        return Results.NoContent();
    }

    internal static async Task<PlanningFeature?> LoadFeatureWithArtifacts(
        OvermindDbContext db, Guid projectId, string slug, CancellationToken ct)
    {
        return await db.PlanningFeatures
            .Include(f => f.Artifacts)
            .ThenInclude(a => a.Versions)
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Slug == slug, ct);
    }

    internal static async Task<IReadOnlyDictionary<int, TaskBlockedState>> BlockedStatesAsync(
        TaskDependencyService taskDependencies, PlanningFeature feature, CancellationToken ct) =>
        (await taskDependencies.BlockedOrdersAsync(feature.Id, ct)).ToDictionary(s => s.Order);

    internal static bool TryParseEnum<TEnum>(string value, out TEnum parsed) where TEnum : struct, Enum
    {
        return Enum.TryParse(value, ignoreCase: true, out parsed) && Enum.IsDefined(parsed);
    }

    internal static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<FeatureOverlapRow>>> OverlapIndexAsync(
        OvermindDbContext db, Guid projectId, CancellationToken ct) =>
        CodeSurfaceOverlapProjection.Compute(await db.PlanningFeatures
            .AsNoTracking()
            .Where(f => f.ProjectId == projectId)
            .Select(f => new CodeSurfaceFeatureRow(f.Id, f.Slug, f.Status, f.ClaimSessionId, f.CodeSurfaces))
            .ToListAsync(ct));

    private static IResult InvalidValue(string field, string[] allowed) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [field] = [$"Must be one of: {string.Join(", ", allowed.Select(n => n.ToLowerInvariant()))}."],
        });
}
