using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
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
        IValidator<OnboardRequest> validator,
        OvermindDbContext db,
        PlanningPolicy policy,
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
            else
            {
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

        return Results.Ok(new OnboardResponse(
            project.Id,
            project.Name,
            created,
            board,
            new OnboardMaterializationBlock(
                StrategyText(strategy),
                TargetDir(project, strategy),
                TriggerText(strategy),
                project.LastMaterializedAt,
                project.LastMaterializationError,
                project.LastMaterializationFailedAt),
            new OnboardValidationBlock(humanGate),
            OnboardQuickstart.Text));
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

    private static string TriggerText(PlanningMaterialization strategy) =>
        strategy == PlanningMaterialization.Off
            ? "Materialization is off for this project — the materializer will not run. Enable it via the planning.materializationDefault setting or the per-project override."
            : "Event-driven: runs automatically after every planning mutation.";

    private static async Task<OnboardBoardSummary> BuildBoardSummaryAsync(
        OvermindDbContext db, Guid projectId, CancellationToken ct)
    {
        var features = await db.PlanningFeatures
            .Where(f => f.ProjectId == projectId)
            .Select(f => new { f.Status, f.Slug, f.IsCurrent })
            .ToListAsync(ct);

        var countsByStatus = features
            .GroupBy(f => f.Status)
            .ToDictionary(g => g.Key, g => g.Count());
        var currentSlug = features.FirstOrDefault(f => f.IsCurrent)?.Slug;

        return new OnboardBoardSummary(countsByStatus, currentSlug);
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
