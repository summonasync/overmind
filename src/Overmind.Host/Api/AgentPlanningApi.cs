using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Mapping;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public static class AgentPlanningApi
{
    internal static Task<IResult> CreateArtifact(
        Guid projectId,
        string slug,
        CreatePlanningArtifactRequest request,
        IValidator<CreatePlanningArtifactRequest> validator,
        PlanningFeatureService featureService,
        PlanningArtifactService artifactService,
        OvermindDbContext db,
        CancellationToken ct) =>
        PlanningArtifactsApi.CreateArtifactForDoor(
            projectId, slug, request, validator, featureService, artifactService, db,
            PlanningLogSource.Agent, ct);

    internal static Task<IResult> AppendLog(
        Guid id,
        AppendPlanningLogRequest request,
        IValidator<AppendPlanningLogRequest> validator,
        PlanningArtifactService service,
        CancellationToken ct) =>
        PlanningArtifactsApi.AppendLogForDoor(id, request, validator, service, PlanningLogSource.Agent, ct);

    internal static Task<IResult> EditLog(
        Guid id,
        EditPlanningLogRequest request,
        IValidator<EditPlanningLogRequest> validator,
        PlanningArtifactService service,
        CancellationToken ct) =>
        PlanningArtifactsApi.EditLogForDoor(id, request, validator, service, PlanningLogSource.Agent, ct);

    private static RouteHandlerBuilder LockToClaim(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(ClaimLockFilter.Instance);

    public static RouteGroupBuilder MapAgentPlanningApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/agent");

        group.MapGet("/projects/{projectId:guid}/planning/features", PlanningFeaturesApi.Board);
        group.MapPost("/projects/{projectId:guid}/planning/features", PlanningFeaturesApi.CreateFeature);
        group.MapGet("/projects/{projectId:guid}/planning/features/{slug}", PlanningFeaturesApi.GetFeature);
        group.MapGet("/projects/{projectId:guid}/planning/task-queue", PlanningQueueApi.QueueRead);
        group.MapGet("/projects/{projectId:guid}/planning/features/{slug}/current", Current);
        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/status", SetFeatureStatus).LockToClaim();
        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/update", UpdateFeature).LockToClaim();
        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/queue-move", PlanningQueueApi.QueueMove).LockToClaim();
        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/priority", SetFeaturePriority).LockToClaim();
        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/claim", Claim);
        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/unclaim", Unclaim);
        group.MapGet("/projects/{projectId:guid}/planning/features/{slug}/dependencies", PlanningDependenciesApi.List);
        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/dependencies", PlanningDependenciesApi.Add).LockToClaim();
        group.MapDelete("/projects/{projectId:guid}/planning/features/{slug}/dependencies/{blockedBySlug}", PlanningDependenciesApi.Remove).LockToClaim();
        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/artifacts", CreateArtifact).LockToClaim();
        group.MapGet("/projects/{projectId:guid}/planning/features/{slug}/artifacts", PlanningFeaturesApi.ListArtifacts);
        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/artifacts/tasks", PlanningArtifactsApi.CreateTasks).LockToClaim();
        group.MapGet("/planning/artifacts/{id:guid}", GetArtifact);
        group.MapGet("/planning/artifacts/{id:guid}/diff", PlanningArtifactsApi.Diff);
        group.MapPost("/planning/artifacts/{id:guid}/status", SetArtifactStatus).LockToClaim();
        group.MapPost("/planning/artifacts/{id:guid}/log", AppendLog).LockToClaim();
        group.MapPut("/planning/artifacts/{id:guid}/log", EditLog).LockToClaim();
        group.MapPost("/planning/artifacts/{id:guid}/patch", Patch).LockToClaim();
        group.MapDelete("/planning/artifacts/{id:guid}", DeleteArtifact).LockToClaim();
        group.MapPost("/projects/{projectId:guid}/planning/import", ImportFeature);
        group.MapGet("/projects/{projectId:guid}/planning/materialization-status", MaterializationStatus);
        group.MapPost("/projects/{projectId:guid}/planning/export", PlanningExportApi.Export);
        group.MapPost("/planning/artifacts/{id:guid}/validate", Validate).LockToClaim();

        return group;
    }

    // The lock's entry point: claim and unclaim reach FeatureClaimService unfiltered so the
    // take-over/adopt matrix decides, not the generic mutation lock.
    internal static async Task<IResult> Claim(
        Guid projectId,
        string slug,
        ClaimPlanningFeatureRequest request,
        FeatureClaimService claims,
        HttpRequest http,
        CancellationToken ct)
    {
        if (!SessionHeader.TryRead(http, out var sessionId))
        {
            return HttpErrors.BadRequest(
                $"Claiming requires the {SessionHeader.Name} header — a claim binds to the adapter session.");
        }

        try
        {
            var feature = await claims.ClaimAsync(projectId, slug, request.Alias, request.Vendor, sessionId, ct);
            return Results.Ok(await FeatureClaimEcho.ForAsync(claims, projectId, feature, ct));
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
        HttpRequest http,
        CancellationToken ct)
    {
        if (!SessionHeader.TryRead(http, out var sessionId))
        {
            return HttpErrors.BadRequest(
                $"Unclaiming requires the {SessionHeader.Name} header — only the holder session releases a claim through this door.");
        }

        try
        {
            var feature = await claims.UnclaimAsync(projectId, slug, sessionId, ct);
            return Results.Ok(await FeatureClaimEcho.ForAsync(claims, projectId, feature, ct));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    internal static async Task<IResult> SetFeaturePriority(
        Guid projectId,
        string slug,
        SetPlanningFeaturePriorityRequest request,
        PlanningFeatureService service,
        FeatureClaimService claims,
        FeatureDependencyService dependencies,
        TaskDependencyService taskDependencies,
        OvermindDbContext db,
        CancellationToken ct)
    {
        if (!PlanningFeaturesApi.TryParseEnum(request.Priority, out FeaturePriority parsed))
        {
            return HttpErrors.BadRequest(
                $"Unknown priority '{request.Priority}'. Allowed values: p0, p1, p2, p3.");
        }

        try
        {
            await service.UpdateAsync(projectId, slug, null, null, null, parsed, ct);
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }

        var feature = await PlanningFeaturesApi.LoadFeatureWithArtifacts(db, projectId, slug, ct);
        return Results.Ok(PlanningMapper.ToFeatureResponse(
            feature!,
            await dependencies.ListDependenciesAsync(projectId, slug, ct),
            await PlanningFeaturesApi.BlockedStatesAsync(taskDependencies, feature!, ct),
            (await claims.ClaimLivenessAsync(projectId, [feature!], ct)).GetValueOrDefault(feature!.Id)));
    }

    internal static async Task<IResult> ImportFeature(
        Guid projectId,
        ImportPlanningFeatureRequest request,
        IValidator<ImportPlanningFeatureRequest> validator,
        BacklogImporter importer,
        FeatureClaimService claims,
        OvermindDbContext db,
        HttpRequest http,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null)
        {
            return HttpErrors.NotFound($"Project '{projectId}' was not found.");
        }

        try
        {
            SessionHeader.TryRead(http, out var sessionId);
            var imported = await importer.ParseFeatureAsync(
                projectId, project.DiskPath, request.Path.Trim(), ct);
            await claims.EnsureImportAllowedAsync(projectId, sessionId, imported.Slug, ct);

            var report = await importer.ImportFeatureAsync(projectId, project.DiskPath, request.Path.Trim(), ct);
            return Results.Ok(PlanningMapper.ToImportResponse(report));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    internal static async Task<IResult> MaterializationStatus(
        Guid projectId,
        PlanningMaterializationStatusReader reader,
        CancellationToken ct)
    {
        PlanningMaterializationStatus status;
        try
        {
            status = await reader.ReadAsync(projectId, ct);
        }
        catch (MaterializationException ex) when (ex.Kind is MaterializationFailureKind.NotFound)
        {
            return HttpErrors.NotFound(ex.Message);
        }

        return Results.Ok(new PlanningMaterializationStatusResponse(
            status.ProjectId,
            status.LastSuccess,
            status.LastAttempt,
            status.LastAttemptReason,
            status.LastRun is null
                ? null
                : new PlanningMaterializationStatusRunResponse(
                    status.LastRun.StartedAt,
                    status.LastRun.FinishedAt,
                    status.LastRun.Atomic,
                    status.LastRun.Written,
                    ToSkipResponses(status.LastRun.Skipped),
                    ToSkipResponses(status.LastRun.Refused)),
            status.TrackedFiles,
            [.. status.StalePaths],
            status.Strategy,
            status.TargetDir));
    }

    private static PlanningMaterializationSkipResponse[] ToSkipResponses(
        IReadOnlyList<PlanningMaterializationSkip> skips) =>
        [.. skips.Select(s => new PlanningMaterializationSkipResponse(s.RelativePath, s.Reason))];

    internal static async Task<IResult> Validate(
        Guid id,
        ValidatePlanningArtifactRequest? request,
        PlanningArtifactService artifactService,
        PlanningPolicy policy,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var asMigrated = request?.AsMigrated == true;

        if (asMigrated && string.IsNullOrWhiteSpace(request!.LogNote))
        {
            return PlanningErrors.Map(PlanningException.MigratedNoteRequired());
        }

        var target = await db.PlanningArtifacts
            .Where(a => a.Id == id)
            .Select(a => new
            {
                a.Kind,
                FeatureSlug = db.PlanningFeatures
                    .Where(f => f.Id == a.FeatureId)
                    .Select(f => f.Slug)
                    .First(),
                ProjectId = db.PlanningFeatures
                    .Where(f => f.Id == a.FeatureId)
                    .Select(f => f.ProjectId)
                    .First(),
            })
            .FirstOrDefaultAsync(ct);

        if (target is null)
        {
            return HttpErrors.NotFound(
                $"Artifact '{id}' was not found. {PlanningException.ArtifactNotFoundHint}");
        }

        if (!asMigrated && await policy.ResolveValidationGateAsync(target.ProjectId, ct))
        {
            return PlanningErrors.Map(PlanningException.HumanValidationRequired(target.Kind, target.FeatureSlug));
        }

        var migrationNote = asMigrated
            ? $"validated as migrated — pre-approved on disk: {request!.LogNote!.Trim()}"
            : null;
        var documentGated = target.Kind is PlanningArtifactKind.Spec or PlanningArtifactKind.Plan;

        PlanningArtifact artifact;
        try
        {
            artifact = await artifactService.ValidateAsync(
                id, migrationNote, PlanningLogSource.Agent,
                requireHumanProof: !asMigrated && documentGated, ct: ct);
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }

        return Results.Ok(PlanningMapper.ToMutationResponse(artifact));
    }

    internal static async Task<IResult> GetArtifact(
        Guid id, string? sections, OvermindDbContext db, CancellationToken ct)
    {
        var artifact = await db.PlanningArtifacts
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (artifact is null)
        {
            return HttpErrors.NotFound(
                $"Artifact '{id}' was not found. {PlanningException.ArtifactNotFoundHint}");
        }

        var requested = RequestedSections(sections);
        if (requested.Count == 0)
        {
            return Results.Ok(new PlanningArtifactReadResponse(
                artifact.Id, artifact.Kind, artifact.Status, artifact.Version,
                artifact.Order, artifact.UpdatedAt, artifact.Brief, artifact.Body));
        }

        var narrowed = new StringBuilder();
        var missing = new List<string>();

        foreach (var name in requested)
        {
            if (PlanningSections.Find(name, artifact.Body) is not { } section)
            {
                missing.Add(name);
                continue;
            }

            if (narrowed.Length > 0)
            {
                narrowed.Append('\n');
            }

            narrowed.Append('<').Append(section.Name).Append(">\n")
                .Append(section.Content)
                .Append("\n</").Append(section.Name).Append('>');
        }

        return Results.Ok(new PlanningArtifactReadResponse(
            artifact.Id, artifact.Kind, artifact.Status, artifact.Version,
            artifact.Order, artifact.UpdatedAt, artifact.Brief,
            narrowed.ToString(), [.. missing]));
    }

    private static List<string> RequestedSections(string? sections)
    {
        if (string.IsNullOrWhiteSpace(sections))
        {
            return [];
        }

        return sections
            .Split([',', ' ', '\t'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct()
            .ToList();
    }

    internal static async Task<IResult> SetArtifactStatus(
        Guid id,
        SetPlanningArtifactStatusRequest request,
        IValidator<SetPlanningArtifactStatusRequest> validator,
        PlanningArtifactService service,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        try
        {
            var result = await service.SetStatusAsync(
                id, request.Status, PlanningLogSource.Agent, request.LogNote, ct);
            return Results.Ok(new SetPlanningArtifactStatusResponse(
                result.ArtifactId,
                result.Status,
                result.Version,
                result.LastLogLine,
                result.Warnings,
                result.FeatureStatusChanged));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    internal static async Task<IResult> SetFeatureStatus(
        Guid projectId,
        string slug,
        SetPlanningFeatureStatusRequest request,
        IValidator<SetPlanningFeatureStatusRequest> validator,
        PlanningFeatureService service,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        if (request.Status is not (PlanningFeatureStatus.Done or PlanningFeatureStatus.InProgress))
        {
            return PlanningErrors.Map(PlanningException.FeatureStatusNotAllowedForAgent(request.Status));
        }

        try
        {
            await service.SetStatusAsync(
                projectId, slug, request.Status, request.LogNote, requireHumanTap: true, ct);
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }

        var feature = await db.PlanningFeatures
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Slug == slug, ct);
        return Results.Ok(PlanningMapper.ToMutationResponse(feature!));
    }

    internal static async Task<IResult> UpdateFeature(
        Guid projectId,
        string slug,
        UpdatePlanningFeatureByAgentRequest request,
        IValidator<UpdatePlanningFeatureByAgentRequest> validator,
        PlanningFeatureService service,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        FeaturePriority? priority = request.Priority is null
            ? null
            : Enum.Parse<FeaturePriority>(request.Priority, ignoreCase: true);

        try
        {
            var feature = await service.UpdateAsync(
                projectId, slug, request.Title, request.Body,
                priority, request.LogNote, PlanningLogSource.Agent, ct);

            return Results.Ok(new PlanningFeatureAgentUpdateResponse(
                feature.Slug, feature.Status, feature.Version, feature.UpdatedAt, feature.Body.Length));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    internal static async Task<IResult> Current(
        Guid projectId,
        string slug,
        PlanningPolicy policy,
        FeatureDependencyService dependencies,
        TaskDependencyService taskDependencies,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            return HttpErrors.NotFound($"Project '{projectId}' was not found.");
        }

        var feature = await db.PlanningFeatures
            .Include(f => f.Artifacts)
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Slug == slug, ct);
        if (feature is null)
        {
            return HttpErrors.NotFound($"Feature '{slug}' was not found.");
        }

        var currentFeature = new PlanningCurrentFeature(feature.Slug, feature.Title, feature.Status);

        var openBlockers = await dependencies.OpenBlockersAsync(projectId, slug, ct);
        if (openBlockers.Count > 0)
        {
            return Results.Ok(new PlanningCurrentResponse(
                "stop", currentFeature, null,
                $"STOP: feature '{feature.Slug}' is blocked by unclosed features: {BlockerList(openBlockers)}. " +
                "Reads stay open; task issuance stops until every blocker closes (done or review)."));
        }

        var gated = await policy.ResolveValidationGateAsync(projectId, ct);

        var spec = feature.Artifacts.FirstOrDefault(a => a.Kind == PlanningArtifactKind.Spec);
        var plan = feature.Artifacts.FirstOrDefault(a => a.Kind == PlanningArtifactKind.Plan);

        if (gated && (spec is null || spec.Status == PlanningArtifactStatus.Draft))
        {
            return Results.Ok(new PlanningCurrentResponse(
                "stop", currentFeature, null,
                $"STOP: spec awaits human validation — it cannot be approved through MCP. " +
                $"Send overmind_notify(kind=approval, featureSlug={feature.Slug}) and wait for the human's reply — validation is never agent-approvable."));
        }

        if (gated && spec is { Status: PlanningArtifactStatus.Validated } &&
            (plan is null || plan.Status == PlanningArtifactStatus.Draft))
        {
            return Results.Ok(new PlanningCurrentResponse(
                "stop", currentFeature, null,
                $"STOP: plan awaits human validation — it cannot be approved through MCP. " +
                $"Send overmind_notify(kind=approval, featureSlug={feature.Slug}) and wait for the human's reply — validation is never agent-approvable."));
        }

        var tasks = feature.Artifacts
            .Where(a => a.Kind == PlanningArtifactKind.Task)
            .OrderBy(a => a.Order)
            .ToList();

        if (tasks.Count == 0)
        {
            return Results.Ok(new PlanningCurrentResponse(
                "empty", currentFeature, null,
                "No tasks yet — decompose the validated plan via planning_upsert_tasks: each task an execution contract (goal, scope, anti-scope, acceptance-criteria, refs)."));
        }

        if (tasks.All(t => t.Status == PlanningArtifactStatus.Done))
        {
            return Results.Ok(new PlanningCurrentResponse(
                "empty", currentFeature, null, ResultStateReason(feature)));
        }

        var candidates = tasks
            .Where(t => t.Status is PlanningArtifactStatus.Todo or PlanningArtifactStatus.InProgress)
            .ToList();

        if (candidates.Count > 0)
        {
            var blockedStates = await taskDependencies.BlockedOrdersAsync(feature.Id, ct);
            var blockedOrders = blockedStates
                .Where(s => s.Blocked)
                .Select(s => s.Order)
                .ToHashSet();

            var actionable = candidates.FirstOrDefault(t => !blockedOrders.Contains(t.Order));
            if (actionable is not null)
            {
                return Results.Ok(new PlanningCurrentResponse(
                    "task",
                    currentFeature,
                    PlanningMapper.ToArtifactResponse(
                        actionable, blockedStates.ToDictionary(s => s.Order)),
                    null,
                    SpecPlanWarnings(feature)));
            }

            return Results.Ok(new PlanningCurrentResponse(
                "stop",
                currentFeature,
                null,
                $"STOP: every actionable task is blocked by unclosed tasks: " +
                $"{await UnclosedChainAsync(taskDependencies, candidates, ct)}. " +
                "Reads stay open; task issuance stops until every blocker closes (done or review)."));
        }

        var inReview = tasks.Count(t => t.Status == PlanningArtifactStatus.Review);
        if (inReview > 0)
        {
            return Results.Ok(new PlanningCurrentResponse(
                "held", currentFeature, null,
                $"{inReview} in review — finish the reviews, then close each task with planning_set_status."));
        }

        var blocked = tasks.First(t => t.Status == PlanningArtifactStatus.Blocked);
        return Results.Ok(new PlanningCurrentResponse(
            "task",
            currentFeature,
            PlanningMapper.ToArtifactResponse(blocked),
            "The next task is blocked.",
            SpecPlanWarnings(feature)));
    }

    private static async Task<string> UnclosedChainAsync(
        TaskDependencyService taskDependencies,
        IReadOnlyList<PlanningArtifact> candidates,
        CancellationToken ct)
    {
        var links = new List<string>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var blockers = await taskDependencies.UnclosedBlockersAsync(candidate.Id, ct);
            links.Add($"task {candidate.Order} blocked by [{string.Join(", ", blockers.Select(b => b.Order))}]");
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

    internal static async Task<IResult> DeleteArtifact(
        Guid id,
        [FromBody] DeletePlanningArtifactRequest? request,
        PlanningArtifactService service,
        OvermindDbContext db,
        CancellationToken ct)
    {
        PlanningArtifact artifact;
        try
        {
            artifact = await service.DeleteAsync(
                id,
                byHuman: false,
                request?.LogNote,
                PlanningDoorSource.Resolve(request?.Source, PlanningLogSource.Agent),
                ct);
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }

        var feature = await db.PlanningFeatures.FirstAsync(f => f.Id == artifact.FeatureId, ct);
        return Results.Ok(new PlanningArtifactDeleteResponse(artifact.Id, artifact.Kind, feature.LastLogLine));
    }

    internal static async Task<IResult> Patch(
        Guid id,
        PatchPlanningArtifactRequest request,
        PlanningArtifactService service,
        CancellationToken ct)
    {
        if (!CarriesPatchShape(request))
        {
            return PlanningErrors.Map(PlanningException.InvalidPatchRequest());
        }

        try
        {
            var op = request.Op!.Trim();
            var flipsAc = op is "tick_ac" or "untick_ac";
            var result = await service.PatchAsync(
                id,
                new PlanningArtifactPatch(
                    request.Section, request.Content, request.AcIndex, request.AcText,
                    request.Brief, request.LogNote,
                    Untick: op == "untick_ac"),
                ct,
                PlanningDoorSource.Resolve(request.Source, PlanningLogSource.Agent));
            var artifact = result.Artifact;

            return Results.Ok(new PlanningArtifactPatchResponse(
                artifact.Id,
                artifact.Kind,
                flipsAc ? "acceptance-criteria" : op == "set_brief" ? "brief" : request.Section!,
                artifact.Version,
                artifact.Body.Length,
                artifact.Status,
                artifact.LastLogLine,
                artifact.StatusChanged,
                artifact.FeatureStatusChanged,
                flipsAc ? result.TickedIndex : null,
                flipsAc ? result.TickedText : null,
                flipsAc ? result.NoChange : null));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    private static string BlockerList(IReadOnlyList<FeatureDependencyEntry> openBlockers) =>
        string.Join(", ", openBlockers.Select(FormatBlocker));

    private static string FormatBlocker(FeatureDependencyEntry entry) =>
        $"{entry.Slug} ({entry.Status.ToString().ToLowerInvariant()})";

    private static bool CarriesPatchShape(PatchPlanningArtifactRequest request)
    {
        var op = request.Op?.Trim();

        if (op == "set_brief")
        {
            return request.Section is null &&
                   request.Content is null &&
                   request.AcIndex is null &&
                   request.AcText is null &&
                   !string.IsNullOrWhiteSpace(request.Brief);
        }

        if (op == "replace_section")
        {
            return request.Brief is null &&
                   !string.IsNullOrWhiteSpace(request.Section) &&
                   !string.IsNullOrWhiteSpace(request.Content) &&
                   request.AcIndex is null &&
                   request.AcText is null;
        }

        if (op is "tick_ac" or "untick_ac")
        {
            return request.Brief is null &&
                   request.Section is null &&
                   request.Content is null &&
                   (request.AcIndex is not null) != (request.AcText is not null) &&
                   (request.AcText is null || !string.IsNullOrWhiteSpace(request.AcText));
        }

        return false;
    }
}
