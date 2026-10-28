using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public static class OnboardApi
{
    public static RouteGroupBuilder MapOnboardApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");

        group.MapPost("/onboard", Onboard);

        return group;
    }

    internal static async Task<IResult> Onboard(
        OnboardRequest request,
        HttpRequest http,
        IValidator<OnboardRequest> validator,
        OvermindDbContext db,
        PlanningPolicy policy,
        ISemanticSearch semanticSearch,
        AppSettingsService settings,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        Project project;
        var created = false;

        if (request.ProjectId is { } projectId)
        {
            var existing = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
            if (existing is null)
            {
                return HttpErrors.NotFound($"Project '{projectId}' was not found.");
            }

            project = existing;
        }
        else
        {
            string normalizedCwd;
            try
            {
                normalizedCwd = NormalizeCwd(request.Cwd);
            }
            catch (ArgumentException)
            {
                return HttpErrors.BadRequest($"Cwd '{request.Cwd}' is not a valid path.");
            }

            var matched = await db.Projects
                .FirstOrDefaultAsync(p => p.DiskPath == normalizedCwd, ct);

            if (matched is not null)
            {
                project = matched;
            }
            else if (!request.Create)
            {
                return HttpErrors.NotFound(
                    $"No project is registered for the working directory '{normalizedCwd}'. Call overmind_onboard to register it, or pass projectId explicitly.");
            }
            else if (TransientPathClassifier.IsTransient(normalizedCwd))
            {
                return HttpErrors.BadRequest(
                    $"The working directory '{normalizedCwd}' is a transient path; projects are never registered from there. Run from the project root or set OVERMIND_PROJECT_ID.");
            }
            else
            {
                var refusal = await WorktreeRefusalAsync(normalizedCwd, db, ct);
                if (refusal is not null)
                {
                    return refusal;
                }

                var name = await ResolveUniqueNameAsync(new DirectoryInfo(normalizedCwd).Name, db, ct);
                project = Project.Create(name, normalizedCwd);
                db.Projects.Add(project);
                await db.SaveChangesAsync(ct);
                created = true;
            }
        }

        var board = await BuildBoardSummaryAsync(db, project.Id, ct);
        var strategy = await policy.ResolveMaterializationAsync(project.Id, ct);
        var humanGate = await policy.ResolveValidationGateAsync(project.Id, ct);
        var vectorHealth = await semanticSearch.HealthReportAsync(ct);
        var inlinePass = string.Equals(
            await settings.GetOrDefaultAsync(
                SettingKeys.PlanningMaterializationSync,
                SettingDefaults.PlanningMaterializationSync, ct),
            "on", StringComparison.OrdinalIgnoreCase);

        return Results.Ok(new OnboardResponse(
            project.Id,
            project.Name,
            created,
            board,
            new OnboardMaterializationBlock(
                StrategyText(strategy),
                TargetDir(project, strategy),
                TriggerText(strategy, inlinePass),
                project.LastMaterializedAt,
                project.LastMaterializationAttemptAt,
                project.LastMaterializationError,
                project.LastMaterializationFailedAt,
                project.LastMaterializationSkipCount,
                SkipHeadLines(project.LastMaterializationSkipHead)),
            new OnboardValidationBlock(humanGate, ValidationGateGuidance(humanGate)),
            new OnboardSemanticBlock(vectorHealth.Up, SemanticUnavailableReason(vectorHealth)),
            new OnboardAgentIdentityBlock(
                AgentIdentityDefaults.Alias,
                AgentIdentityDefaults.Vendor,
                await policy.ResolveAgentNamingRulesAsync(project, ct) ?? AgentIdentityDefaults.GenericNamingHint),
            BuildAdapterBlock(http),
            OnboardQuickstart.Text));
    }

    private static async Task<IResult?> WorktreeRefusalAsync(string normalizedCwd, OvermindDbContext db, CancellationToken ct)
    {
        if (WorktreeProbe.Probe(normalizedCwd) is not { } worktree)
        {
            return null;
        }

        string mainRoot;
        try
        {
            mainRoot = NormalizeCwd(worktree.MainRoot);
        }
        catch (ArgumentException)
        {
            return null;
        }

        var owner = await db.Projects.FirstOrDefaultAsync(p => p.DiskPath == mainRoot, ct);
        if (owner is null)
        {
            return null;
        }

        var pinPath = Path.Combine(worktree.Root, ".overmind", "project");
        return HttpErrors.Conflict(
            $"'{normalizedCwd}' is a git worktree of the registered project '{owner.Name}' ({mainRoot}). " +
            "Refusing to auto-create a duplicate project. Pin the worktree instead: write the project id to " +
            $"'{pinPath}' (a raw GUID line or {{ \"projectId\": \"...\" }}) or set OVERMIND_PROJECT_ID, then retry.");
    }

    private static OnboardAdapterBlock BuildAdapterBlock(HttpRequest http)
    {
        var adapterVersion = AdapterVersionHeader.Read(http);
        return new OnboardAdapterBlock(
            adapterVersion,
            AdapterVersionCheck.HostVersion,
            AdapterVersionCheck.Warning(adapterVersion, AdapterVersionCheck.HostVersion));
    }

    private static string? SemanticUnavailableReason(VectorSearchHealthReport report)
    {
        if (report.Up)
        {
            return null;
        }

        var reasons = new[] { report.Qdrant.Error, report.Embedding.Error }
            .Where(error => !string.IsNullOrEmpty(error))
            .ToArray();

        return reasons.Length > 0 ? string.Join("; ", reasons) : "probe reported no failure reason";
    }

    private static string StrategyText(PlanningMaterialization strategy) => strategy switch
    {
        PlanningMaterialization.On => "on",
        PlanningMaterialization.Archive => "archive",
        _ => "off",
    };

    private static string TargetDir(Project project, PlanningMaterialization strategy) =>
        strategy == PlanningMaterialization.Archive
            ? PlanningArchive.Root(OvermindPaths.DataDirectory, project)
            : Path.Combine(project.DiskPath, "BACKLOG");

    private static string TriggerText(PlanningMaterialization strategy, bool inline) =>
        strategy == PlanningMaterialization.Off
            ? "Materialization is off for this project — the materializer will not run. Enable it via the planning.materializationDefault setting or the per-project override."
            : inline
                ? "Event-driven: every planning mutation runs the mirror pass inline before returning (planning.materializationSync=on) — the manual planning_materialize tool does the same and waits for its report."
                : "Event-driven: enqueued after every planning mutation, runs shortly after in the background — the write returns before the mirror updates.";

    private static string ValidationGateGuidance(bool humanGate) => humanGate
        ? "agents STOP until a human validates the artifact in the UI."
        : "agent validate requires a live approval proof: overmind_notify(kind=approval) with a human reply " +
          "newer than the artifact's last update; without it planning_validate returns 409 ValidationProofRequired. " +
          "humanGate=on would STOP agents at the UI door instead.";

    private static IReadOnlyList<string>? SkipHeadLines(string? skipHead) =>
        skipHead is null
            ? null
            : skipHead.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static async Task<OnboardBoardSummary> BuildBoardSummaryAsync(
        OvermindDbContext db, Guid projectId, CancellationToken ct)
    {
        var countsByStatus = await db.PlanningFeatures
            .Where(f => f.ProjectId == projectId)
            .GroupBy(f => f.Status)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);

        return new OnboardBoardSummary(countsByStatus);
    }

    private static async Task<string> ResolveUniqueNameAsync(
        string baseName, OvermindDbContext db, CancellationToken ct)
    {
        var existingNames = await db.Projects.Select(p => p.Name).ToListAsync(ct);
        var name = baseName;
        var suffix = 1;
        while (existingNames.Contains(name, StringComparer.Ordinal))
        {
            suffix++;
            name = $"{baseName}-{suffix}";
        }

        return name;
    }

    private static string NormalizeCwd(string cwd)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(cwd);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            throw new ArgumentException($"Cwd '{cwd}' is not a valid path.", nameof(cwd), ex);
        }

        return TrimTrailingSeparators(ResolveSymlinks(TrimTrailingSeparators(fullPath)));
    }

    private static string ResolveSymlinks(string path)
    {
        try
        {
            return new DirectoryInfo(path).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? path;
        }
        catch (Exception)
        {
            return path;
        }
    }

    private static string TrimTrailingSeparators(string path)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return trimmed.Length == 0 ? path : trimmed;
    }
}
