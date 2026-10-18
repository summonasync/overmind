using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Host.Api;

public static class SystemRootsApi
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static RouteGroupBuilder MapSystemRootsApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/system");
        group.MapGet("/tree-roots", GetTreeRoots);
        group.MapPost("/tree-roots", AddTreeRoot);
        group.MapDelete("/tree-roots", RemoveTreeRoot);
        return group;
    }

    private static async Task<IResult> GetTreeRoots(
        AppSettingsService settings,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var roots = await ReadRootsAsync(settings, ct);
        return Results.Ok(await BuildResponseAsync(roots, db, ct));
    }

    private static async Task<IResult> AddTreeRoot(
        AddTreeRootRequest request,
        AppSettingsService settings,
        OvermindDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
        {
            return HttpErrors.BadRequest("A 'path' is required.");
        }

        if (!Path.IsPathRooted(request.Path))
        {
            return HttpErrors.BadRequest($"'{request.Path}' is not an absolute path.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(request.Path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return HttpErrors.BadRequest($"'{request.Path}' is not a valid path: {ex.Message}");
        }

        if (File.Exists(fullPath))
        {
            return HttpErrors.BadRequest($"'{fullPath}' is not a directory.");
        }

        if (!Directory.Exists(fullPath))
        {
            return HttpErrors.NotFound($"'{fullPath}' does not exist or is not a directory.");
        }

        var normalized = SystemApi.NormalizePath(fullPath);
        var comparison = PathComparison;
        var roots = await ReadRootsAsync(settings, ct);

        foreach (var existing in roots)
        {
            if (existing.Equals(normalized, comparison))
            {
                return HttpErrors.Conflict($"'{normalized}' is already a root folder.");
            }

            if (IsSameOrUnder(normalized, existing, comparison))
            {
                return HttpErrors.Conflict($"'{normalized}' is already covered by root '{existing}'.");
            }
        }

        roots.RemoveAll(existing => IsSameOrUnder(existing, normalized, comparison));
        roots.Add(normalized);
        await settings.SetAsync(SettingKeys.TreeRoots, JsonSerializer.Serialize(roots, JsonOptions), ct);

        return Results.Ok(await BuildResponseAsync(roots, db, ct));
    }

    private static async Task<IResult> RemoveTreeRoot(
        string? path,
        AppSettingsService settings,
        OvermindDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return HttpErrors.BadRequest("Query parameter 'path' is required.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return HttpErrors.BadRequest($"'{path}' is not a valid path: {ex.Message}");
        }

        var normalized = SystemApi.NormalizePath(fullPath);
        var comparison = PathComparison;
        var roots = await ReadRootsAsync(settings, ct);
        var index = roots.FindIndex(r => r.Equals(normalized, comparison));
        if (index < 0)
        {
            return HttpErrors.NotFound($"'{normalized}' is not a root folder.");
        }

        var projectPaths = await GetProjectPathsAsync(db, ct);
        if (projectPaths.Any(p => IsSameOrUnder(p, roots[index], comparison)))
        {
            return HttpErrors.Conflict(
                $"'{roots[index]}' contains registered projects and cannot be removed.",
                title: "RootContainsProjects");
        }

        roots.RemoveAt(index);
        await settings.SetAsync(SettingKeys.TreeRoots, JsonSerializer.Serialize(roots, JsonOptions), ct);

        return Results.Ok(await BuildResponseAsync(roots, db, ct));
    }

    private static async Task<TreeRootsResponse> BuildResponseAsync(
        List<string> roots,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var comparison = PathComparison;
        var projectPaths = await GetProjectPathsAsync(db, ct);

        return new TreeRootsResponse(roots.Select(root =>
        {
            var name = Path.GetFileName(root);
            var containsProjects = projectPaths.Any(p => IsSameOrUnder(p, root, comparison));
            return new TreeRootResponse(string.IsNullOrEmpty(name) ? root : name, root, containsProjects);
        }).ToArray());
    }

    internal static async Task<List<string>> ReadRootsAsync(AppSettingsService settings, CancellationToken ct)
    {
        var raw = await settings.GetAsync(SettingKeys.TreeRoots, ct);
        return string.IsNullOrWhiteSpace(raw)
            ? []
            : JsonSerializer.Deserialize<List<string>>(raw, JsonOptions) ?? [];
    }

    private static async Task<List<string>> GetProjectPathsAsync(OvermindDbContext db, CancellationToken ct)
    {
        var projects = await db.Projects.AsNoTracking().ToListAsync(ct);
        return projects
            .Select(p => SystemApi.NormalizePath(p.DiskPath))
            .ToList();
    }

    private static bool IsSameOrUnder(string path, string root, StringComparison comparison) =>
        path.Equals(root, comparison) ||
        path.StartsWith(root + Path.DirectorySeparatorChar, comparison);

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
