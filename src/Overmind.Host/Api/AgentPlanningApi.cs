using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Mapping;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Notifications;
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
        builder.AddEndpointFilter(OperationReplayFilter.Instance)
            .AddEndpointFilter(ClaimLockFilter.Instance);

    private static async Task<DelegatedApprovalGrant?> ResolveDelegationAsync(
        Guid projectId,
        HttpRequest http,
        EmbeddedAgentSessionState sessionState,
        DelegatedApprovalGate delegation,
        CancellationToken ct)
    {
        if (!SessionHeader.TryRead(http, out var callerSession))
        {
            return null;
        }

        return await delegation.AuthorizeAsync(
            projectId,
            callerSession,
            sessionState.SessionId,
            ReadDelegatedBy(http),
            ct);
    }

    private static Guid? ReadDelegatedBy(HttpRequest http) =>
        http.Headers.TryGetValue(AgentLoopbackClient.DelegatedByHeader, out var raw) &&
        Guid.TryParse(raw[^1], out var parsed)
            ? parsed
            : null;

    public static RouteGroupBuilder MapAgentPlanningApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/agent");

        group.MapGet("/projects/{projectId:guid}/planning/features", PlanningFeaturesApi.Board);
        group.MapPost("/projects/{projectId:guid}/planning/features", PlanningFeaturesApi.CreateFeature);
        group.MapGet("/projects/{projectId:guid}/planning/features/{slug}", PlanningFeaturesApi.GetFeature);
        group.MapGet("/projects/{projectId:guid}/planning/task-queue", PlanningQueueApi.QueueRead);
        group.MapGet("/projects/{projectId:guid}/planning/wave-snapshot", WaveSnapshotApi.Snapshot);
        group.MapGet("/projects/{projectId:guid}/planning/rescue-candidates", RescueCandidatesApi.Candidates);
        group.MapGet("/projects/{projectId:guid}/planning/features/{slug}/current", Current);
        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/status", SetFeatureStatus).LockToClaim();
        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/update", UpdateFeature).LockToClaim();
        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/patch", PatchFeature).LockToClaim();
        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/queue-move", PlanningQueueApi.QueueMove).LockToClaim();
        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/priority", SetFeaturePriority).LockToClaim();
        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/pin-code", PlanningCodePinApi.PinCodeForAgent).LockToClaim();
        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/batch", Batch).LockToClaim();
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
            var feature = await claims.ClaimAsync(
                projectId, slug, request.Alias, request.Vendor, sessionId, request.OverrideLaneCheck, ct);
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
            return Results.Ok(await FeatureClaimEcho.ForAsync(claims, projectId, feature, overrideLaneCheck: false, ct));
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
        DirectiveService directives,
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
            await service.UpdateAsync(projectId, slug, null, null, null, parsed, lane: null, codeSurfaces: null, ct);
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
            (await claims.ClaimLivenessAsync(projectId, [feature!], ct)).GetValueOrDefault(feature!.Id),
            openDirectives: await directives.ListOpenAsync(feature!.Id, ct)));
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
            await claims.EnsureImportAllowedAsync(
                projectId, sessionId, imported.Slug, SessionTouchMiddleware.PreTouchLastSeenOf(http), ct);

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
        DelegatedApprovalGate delegation,
        EmbeddedAgentSessionState sessionState,
        OvermindDbContext db,
        HttpRequest http,
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

        string? delegatedNote = null;
        if (!asMigrated && documentGated &&
            await ResolveDelegationAsync(target.ProjectId, http, sessionState, delegation, ct) is { } grant)
        {
            delegatedNote = DelegatedApprovalGate.ProvenanceLine(grant);
        }

        PlanningArtifact artifact;
        try
        {
            artifact = await artifactService.ValidateAsync(
                id,
                migrationNote ?? delegatedNote,
                PlanningLogSource.Agent,
                requireHumanProof: delegatedNote is null && !asMigrated && documentGated,
                ct: ct);
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }

        return Results.Ok(PlanningMapper.ToMutationResponse(artifact));
    }

    internal static async Task<IResult> GetArtifact(
        Guid id, string? sections, OvermindDbContext db,
        TaskDependencyService taskDependencies, CancellationToken ct)
    {
        var artifact = await db.PlanningArtifacts
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (artifact is null)
        {
            return HttpErrors.NotFound(
                $"Artifact '{id}' was not found. {PlanningException.ArtifactNotFoundHint}");
        }

        var blockedBy = await BlockedOrdersForReadAsync(taskDependencies, artifact, ct);

        var requested = RequestedSections(sections);
        if (requested.Count == 0)
        {
            return Results.Ok(new PlanningArtifactReadResponse(
                artifact.Id, artifact.Kind, artifact.Status, artifact.Version,
                artifact.Order, artifact.UpdatedAt, artifact.Brief, artifact.Body, blockedBy));
        }

        var filtered = PlanningSections.FilteredRead(artifact.Body, requested);

        return Results.Ok(new PlanningArtifactReadResponse(
            artifact.Id, artifact.Kind, artifact.Status, artifact.Version,
            artifact.Order, artifact.UpdatedAt, artifact.Brief,
            filtered.Body, blockedBy, [.. filtered.MissingNames]));
    }

    private static async Task<int[]?> BlockedOrdersForReadAsync(
        TaskDependencyService taskDependencies, PlanningArtifact artifact, CancellationToken ct)
    {
        if (artifact.Kind != PlanningArtifactKind.Task)
        {
            return null;
        }

        var state = (await taskDependencies.BlockedOrdersAsync(artifact.FeatureId, ct))
            .First(s => s.Order == artifact.Order);

        return [.. state.BlockedBy];
    }

    internal static List<string> RequestedSections(string? sections)
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
                id, request.Status, PlanningLogSource.Agent, request.LogNote, ct, requireHumanProof: true);
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
        DelegatedApprovalGate delegation,
        EmbeddedAgentSessionState sessionState,
        OvermindDbContext db,
        HttpRequest http,
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

        string? delegatedProvenance = null;
        if (request.Status == PlanningFeatureStatus.Done &&
            await ResolveDelegationAsync(projectId, http, sessionState, delegation, ct) is { } grant)
        {
            delegatedProvenance = DelegatedApprovalGate.ProvenanceLine(grant);
        }

        try
        {
            await service.SetStatusAsync(
                projectId, slug, request.Status, request.LogNote,
                requireHumanTap: delegatedProvenance is null, ct, delegatedProvenance);
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
                priority, request.LogNote, PlanningLogSource.Agent, request.Lane, request.CodeSurfaces, ct);

            return Results.Ok(new PlanningFeatureAgentUpdateResponse(
                feature.Slug, feature.Status, feature.Version, feature.UpdatedAt, feature.Body.Length));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    internal static async Task<IResult> PatchFeature(
        Guid projectId,
        string slug,
        PatchPlanningFeatureRequest request,
        IValidator<PatchPlanningFeatureRequest> validator,
        PlanningFeatureService service,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var section = request.Section.Trim();

        try
        {
            var feature = await service.PatchAsync(
                projectId, slug, section, request.Content,
                request.Remove, request.Reason, request.LogNote,
                PlanningDoorSource.Resolve(request.Source, PlanningLogSource.Agent), ct);

            return Results.Ok(new PlanningFeaturePatchResponse(
                feature.Slug, section, feature.Status, feature.Version,
                feature.Body.Length, feature.LastLogLine));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    internal static async Task<IResult> Batch(
        Guid projectId,
        string slug,
        PlanningBatchRequest request,
        IValidator<PlanningBatchRequest> validator,
        PlanningBatchService batchService,
        OvermindDbContext db,
        BrowserOrigin origin,
        HttpContext context,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        try
        {
            var feature = await PlanningArtifactsApi.ResolveFeatureOrThrow(db, projectId, slug, ct);
            var ops = await MapBatchOpsAsync(feature, request.Ops, db, ct);
            var result = await batchService.ApplyAsync(projectId, slug, ops, ct);
            return Results.Ok(ToBatchResponse(result, request.Ops, origin, context));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    private static async Task<List<PlanningBatchOp>> MapBatchOpsAsync(
        PlanningFeature feature, PlanningBatchOpRequest[] wireOps, OvermindDbContext db, CancellationToken ct)
    {
        var ops = new List<PlanningBatchOp>(wireOps.Length);
        for (var i = 0; i < wireOps.Length; i++)
        {
            ops.Add(await MapBatchOpAsync(feature, i, wireOps[i], db, ct));
        }

        return ops;
    }

    private static async Task<PlanningBatchOp> MapBatchOpAsync(
        PlanningFeature feature, int index, PlanningBatchOpRequest wire, OvermindDbContext db, CancellationToken ct)
    {
        if (!TryParseOpName(wire.Op, out var name))
        {
            throw PlanningException.BatchOp(index, BatchOpUnknown(wire.Op));
        }

        return name switch
        {
            PlanningBatchOpName.Patch => await MapPatchOpAsync(feature, index, wire, db, ct),
            PlanningBatchOpName.AppendLog => new PlanningBatchOp(
                name, ArtifactId: await AddressBatchArtifactAsync(feature, wire, index, db, ct), Note: wire.Note),
            PlanningBatchOpName.SetStatus => await MapSetStatusOpAsync(feature, index, wire, db, ct),
            PlanningBatchOpName.UpsertTasks => new PlanningBatchOp(name, Tasks: MapBatchTasks(wire.Tasks, index)),
            PlanningBatchOpName.Notify => MapNotifyOp(index, wire),
            _ => throw PlanningException.BatchOp(index, BatchOpUnknown(wire.Op)),
        };
    }

    private static PlanningTaskUpsert[]? MapBatchTasks(PlanningTaskUpsertRequest[]? tasks, int index)
    {
        if (tasks is null)
        {
            return null;
        }

        var mapped = new PlanningTaskUpsert[tasks.Length];
        for (var i = 0; i < tasks.Length; i++)
        {
            if (tasks[i] is not { } task)
            {
                throw PlanningException.BatchOp(index, PlanningException.BatchOpInvalid(
                    "upsert_tasks requires every tasks[] entry to be an object carrying a task body."));
            }

            mapped[i] = new PlanningTaskUpsert(
                task.Body, task.Order, task.Review, task.LogNote, task.Brief, task.BlockedBy);
        }

        return mapped;
    }

    private static async Task<PlanningBatchOp> MapPatchOpAsync(
        PlanningFeature feature, int index, PlanningBatchOpRequest wire, OvermindDbContext db, CancellationToken ct)
    {
        var artifactId = await AddressBatchArtifactAsync(feature, wire, index, db, ct);
        var wirePatch = wire.Patch;
        if (wirePatch is null ||
            !CarriesPatchShape(new PatchPlanningArtifactRequest(
                wirePatch.Op, wirePatch.Section, wirePatch.Content, wirePatch.AcIndex,
                wirePatch.AcText, wirePatch.Brief, wirePatch.LogNote, Reason: wirePatch.Reason)))
        {
            throw PlanningException.BatchOp(index, PlanningException.BatchOpInvalid(
                "patch requires the patch payload of its standalone tool: a section with its replacement content, " +
                "an acceptance-criteria tick or un-tick (index or exact text), a brief, or an amendment."));
        }

        var op = wirePatch.Op!.Trim();
        return new PlanningBatchOp(
            PlanningBatchOpName.Patch,
            ArtifactId: artifactId,
            Patch: new PlanningArtifactPatch(
                wirePatch.Section, wirePatch.Content, wirePatch.AcIndex, wirePatch.AcText,
                wirePatch.Brief, wirePatch.LogNote,
                Untick: op == "untick_ac",
                Amend: op == "amend_result",
                Remove: op == "remove_section",
                Reason: wirePatch.Reason));
    }

    private static async Task<PlanningBatchOp> MapSetStatusOpAsync(
        PlanningFeature feature, int index, PlanningBatchOpRequest wire, OvermindDbContext db, CancellationToken ct)
    {
        var artifactId = await AddressBatchArtifactAsync(feature, wire, index, db, ct);

        if (wire.Status is null)
        {
            throw PlanningException.BatchOp(index, PlanningException.BatchOpInvalid(
                "set_status requires the artifact status of its standalone tool."));
        }

        if (!PlanningFeaturesApi.TryParseEnum(wire.Status, out PlanningArtifactStatus status))
        {
            throw PlanningException.BatchOp(index, PlanningException.BatchOpInvalid(
                $"Unknown set_status status '{wire.Status}'. Allowed values: " +
                $"{string.Join(", ", Enum.GetNames<PlanningArtifactStatus>())}."));
        }

        if (string.IsNullOrWhiteSpace(wire.LogNote))
        {
            throw PlanningException.BatchOp(index, PlanningException.BatchOpInvalid(
                "set_status requires the logNote of its standalone tool: a status move without a note is an unattributed rewrite."));
        }

        return new PlanningBatchOp(
            PlanningBatchOpName.SetStatus, ArtifactId: artifactId, Status: status, LogNote: wire.LogNote);
    }

    private static PlanningBatchOp MapNotifyOp(int index, PlanningBatchOpRequest wire)
    {
        if (wire.Kind is null)
        {
            throw PlanningException.BatchOp(index, PlanningException.BatchOpInvalid(
                "notify requires the notification kind of its standalone tool."));
        }

        if (!PlanningFeaturesApi.TryParseEnum(wire.Kind, out NotificationKind kind))
        {
            throw PlanningException.BatchOp(index, PlanningException.BatchOpInvalid(
                $"Unknown notify kind '{wire.Kind}'. Allowed values: {string.Join(", ", Enum.GetNames<NotificationKind>())}."));
        }

        return new PlanningBatchOp(
            PlanningBatchOpName.Notify,
            Kind: kind,
            Text: wire.Text,
            Tab: wire.Tab,
            FeatureSlug: wire.FeatureSlug,
            From: wire.From);
    }

    private static async Task<Guid> AddressBatchArtifactAsync(
        PlanningFeature feature, PlanningBatchOpRequest wire, int index, OvermindDbContext db, CancellationToken ct)
    {
        if (wire.Alias is not null && wire.ArtifactId is not null)
        {
            throw PlanningException.BatchOp(index, PlanningException.BatchOpInvalid(
                "Pass alias or artifactId, not both — alias resolves to the artifact id against the feature roster."));
        }

        if (wire.Alias is { } alias)
        {
            return await ResolveBatchAliasAsync(feature, alias, index, db, ct);
        }

        if (wire.ArtifactId is { } artifactId)
        {
            if (!await db.PlanningArtifacts.AnyAsync(a => a.Id == artifactId, ct))
            {
                throw PlanningException.BatchOp(index, PlanningException.BatchOpInvalid(
                    $"Artifact '{artifactId}' was not found. {PlanningException.ArtifactNotFoundHint}"));
            }

            return artifactId;
        }

        throw PlanningException.BatchOp(index, PlanningException.BatchOpInvalid(
            $"{wire.Op.Trim()} requires the artifact to mutate: pass artifactId or alias — " +
            "ops cannot name artifacts created by earlier ops of the same batch (v1)."));
    }

    private static async Task<Guid> ResolveBatchAliasAsync(
        PlanningFeature feature, string alias, int index, OvermindDbContext db, CancellationToken ct)
    {
        var trimmed = alias.Trim();
        var separator = trimmed.IndexOf('/');
        string? slug = null;
        var suffix = trimmed;
        if (separator >= 0)
        {
            slug = trimmed[..separator];
            suffix = trimmed[(separator + 1)..];
        }

        if ((slug is not null && (slug.Length == 0 || suffix.Length == 0)) ||
            (slug is null && suffix.Length == 0) ||
            !PlanningArtifactAlias.TryParse(suffix, out var wanted))
        {
            throw PlanningException.BatchOp(index, PlanningException.BatchOpInvalid(InvalidAliasText(trimmed)));
        }

        if (slug is not null && !string.Equals(slug, feature.Slug, StringComparison.Ordinal))
        {
            throw PlanningException.BatchOp(index, PlanningException.BatchOpInvalid(
                $"Alias '{trimmed}' addresses feature '{slug}', but the batch is bound to '{feature.Slug}' — " +
                "batch ops stay on the batch feature."));
        }

        var roster = await db.PlanningArtifacts
            .AsNoTracking()
            .Where(a => a.FeatureId == feature.Id)
            .Select(a => new { a.Id, a.Kind, a.Order })
            .ToListAsync(ct);

        var matches = roster
            .Where(a => a.Kind == wanted.Kind && (wanted.Order is not { } expected || a.Order == expected))
            .ToList();

        if (matches.Count == 1)
        {
            return matches[0].Id;
        }

        var present = roster
            .Select(a => $"{a.Kind.ToString().ToLowerInvariant()}/{a.Order}")
            .ToList();

        if (matches.Count == 0)
        {
            throw PlanningException.BatchOp(index, PlanningException.BatchOpInvalid(
                $"No artifact matches alias '{trimmed}'. Present kind/order pairs: " +
                $"{(present.Count == 0 ? "(none)" : string.Join(", ", present))}. " +
                "Ops cannot address artifacts created by earlier ops of the same batch (v1) — " +
                "resolve staged tasks in a follow-up batch."));
        }

        throw PlanningException.BatchOp(index, PlanningException.BatchOpInvalid(
            $"Alias '{trimmed}' is ambiguous: feature '{feature.Slug}' has {matches.Count} " +
            $"{wanted.Kind.ToString().ToLowerInvariant()} artifacts " +
            $"({string.Join(", ", matches.Select(m => $"{m.Kind}@{m.Order}"))}). S/P/R aliases address the single " +
            "document of the kind — duplicate documents are a roster bug; resolve by artifactId."));
    }

    private static PlanningBatchResponse ToBatchResponse(
        PlanningBatchResult result, PlanningBatchOpRequest[] wireOps, BrowserOrigin origin, HttpContext context) =>
        new(
            result.FeatureSlug,
            [.. result.Results.Select((op, i) => ToBatchOpResult(op, i, wireOps[i], origin, context))],
            result.Warnings is { Count: > 0 } ? [.. result.Warnings] : null);

    private static PlanningBatchOpResultResponse ToBatchOpResult(
        PlanningBatchOpResult op, int index, PlanningBatchOpRequest wire, BrowserOrigin origin, HttpContext context)
    {
        object? payload = op.Payload switch
        {
            PlanningArtifactPatchResult patch => PatchEcho(patch, wire.Patch),
            PlanningArtifact artifact => PlanningMapper.ToMutationResponse(artifact),
            PlanningArtifactStatusResult status => new SetPlanningArtifactStatusResponse(
                status.ArtifactId, status.Status, status.Version, status.LastLogLine,
                status.Warnings, status.FeatureStatusChanged),
            PlanningTaskBatchResult tasks => TasksEcho(tasks),
            AgentNotification notification => NotificationMapper.ToAgentResponse(
                notification,
                NotificationLinks.Dashboard(
                    origin.Resolve(context), notification.ProjectId, notification.FeatureSlug, notification.Tab)),
            _ => op.Payload,
        };

        return new PlanningBatchOpResultResponse(OpName(op.Op), index, payload);
    }

    private static PlanningArtifactPatchResponse PatchEcho(
        PlanningArtifactPatchResult result, PlanningBatchPatchRequest? wire)
    {
        var op = wire?.Op?.Trim();
        var flipsAc = op is "tick_ac" or "untick_ac";
        var artifact = result.Artifact;

        return new PlanningArtifactPatchResponse(
            artifact.Id,
            artifact.Kind,
            flipsAc ? "acceptance-criteria" : op == "set_brief" ? "brief" : op == "amend_result" ? "amendment" : wire!.Section!,
            artifact.Version,
            artifact.Body.Length,
            artifact.Status,
            artifact.LastLogLine,
            artifact.StatusChanged,
            artifact.FeatureStatusChanged,
            flipsAc ? result.TickedIndex : null,
            flipsAc ? result.TickedText : null,
            flipsAc ? result.NoChange : null,
            artifact.ValidationProofVoided);
    }

    private static CreatePlanningTasksResponse TasksEcho(PlanningTaskBatchResult result) =>
        new(
            result.Created,
            result.Updated,
            [.. result.Artifacts.Zip(result.BlockedOrders)
                .Select(pair => PlanningMapper.ToArtifactResponse(pair.First) with { BlockedBy = [.. pair.Second] })],
            null);

    private static bool TryParseOpName(string? op, out PlanningBatchOpName name)
    {
        switch (op?.Trim())
        {
            case "patch":
                name = PlanningBatchOpName.Patch;
                return true;
            case "append_log":
                name = PlanningBatchOpName.AppendLog;
                return true;
            case "set_status":
                name = PlanningBatchOpName.SetStatus;
                return true;
            case "upsert_tasks":
                name = PlanningBatchOpName.UpsertTasks;
                return true;
            case "notify":
                name = PlanningBatchOpName.Notify;
                return true;
            default:
                name = default;
                return false;
        }
    }

    private static PlanningException BatchOpUnknown(string? op) => PlanningException.BatchOpInvalid(
        $"Op '{op?.Trim()}' is not in the batch vocabulary: patch, append_log, set_status, upsert_tasks, notify.");

    private static string OpName(PlanningBatchOpName name) => name switch
    {
        PlanningBatchOpName.Patch => "patch",
        PlanningBatchOpName.AppendLog => "append_log",
        PlanningBatchOpName.SetStatus => "set_status",
        PlanningBatchOpName.UpsertTasks => "upsert_tasks",
        PlanningBatchOpName.Notify => "notify",
        _ => name.ToString().ToLowerInvariant(),
    };

    private static string InvalidAliasText(string alias) =>
        $"Invalid artifact alias '{alias}'. Expected '<slug>/T<order>' for a task by order, " +
        "or '<slug>/S', '<slug>/P', '<slug>/R' for the single spec, plan or result — " +
        "the bare forms 'T<order>', 'S', 'P' and 'R' work too since the batch is bound to one feature by the route.";

    internal static async Task<IResult> Current(
        Guid projectId,
        string slug,
        PlanningCurrentService current,
        CancellationToken ct)
    {
        PlanningCurrentResult result;
        try
        {
            result = await current.GetCurrentAsync(projectId, slug, ct);
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }

        return Results.Ok(new PlanningCurrentResponse(
            result.State,
            new PlanningCurrentFeature(
                result.Feature.Slug,
                result.Feature.Title,
                result.Feature.Status,
                PlanningMapper.ToOpenDirectives(result.Feature.OpenDirectives)),
            result.Actionable is null
                ? null
                : PlanningMapper.ToArtifactResponse(result.Actionable, result.BlockedStates),
            result.Reason,
            result.Warnings));
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
                    Untick: op == "untick_ac",
                    Amend: op == "amend_result",
                    Remove: op == "remove_section",
                    Reason: request.Reason),
                ct,
                PlanningDoorSource.Resolve(request.Source, PlanningLogSource.Agent));
            var artifact = result.Artifact;

            return Results.Ok(new PlanningArtifactPatchResponse(
                artifact.Id,
                artifact.Kind,
                flipsAc ? "acceptance-criteria" : op == "set_brief" ? "brief" : op == "amend_result" ? "amendment" : request.Section!,
                artifact.Version,
                artifact.Body.Length,
                artifact.Status,
                artifact.LastLogLine,
                artifact.StatusChanged,
                artifact.FeatureStatusChanged,
                flipsAc ? result.TickedIndex : null,
                flipsAc ? result.TickedText : null,
                flipsAc ? result.NoChange : null,
                artifact.ValidationProofVoided));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

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

        if (op == "amend_result")
        {
            return request.Section is null &&
                   request.AcIndex is null &&
                   request.AcText is null &&
                   request.Brief is null &&
                   !string.IsNullOrWhiteSpace(request.Content);
        }

        if (op == "remove_section")
        {
            return request.Brief is null &&
                   !string.IsNullOrWhiteSpace(request.Section) &&
                   request.Content is null &&
                   request.AcIndex is null &&
                   request.AcText is null;
        }

        if (op == "replace_section")
        {
            return request.Brief is null &&
                   !string.IsNullOrWhiteSpace(request.Section) &&
                   (request.Content is null || !string.IsNullOrWhiteSpace(request.Content)) &&
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
