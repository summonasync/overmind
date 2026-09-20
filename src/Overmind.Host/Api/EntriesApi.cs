using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Mapping;
using Overmind.Infrastructure.Kits;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Search;
using Overmind.Infrastructure.VectorSearch;

namespace Overmind.Host.Api;

public static class EntriesApi
{
    public static RouteGroupBuilder MapEntriesApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");

        group.MapGet("/projects/{projectId:guid}/entries", ListByProject);
        group.MapPost("/entries", Create);
        group.MapGet("/entries/{id:guid}", GetById);
        group.MapPut("/entries/{id:guid}", Update);
        group.MapDelete("/entries/{id:guid}", Delete);
        group.MapGet("/entries/{id:guid}/versions", ListVersions);

        return group;
    }

    private static async Task<IResult> ListByProject(
        Guid projectId,
        string? type,
        string? tag,
        string? q,
        OvermindDbContext db,
        FtsSearchService search,
        CancellationToken ct)
    {
        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            return HttpErrors.NotFound($"Project '{projectId}' was not found.");
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var hits = await search.SearchAsync(projectId, q, 100, ct);
            var hitIds = hits.Select(h => h.EntryId).ToArray();

            var entries = await db.ContextEntries
                .Include(e => e.EntryTags).ThenInclude(t => t.Tag)
                .Include(e => e.SkillFiles)
                .AsSplitQuery()
                .Where(e => hitIds.Contains(e.Id))
                .ToListAsync(ct);

            var responses = entries
                .Where(e => type is null || (Enum.TryParse<EntryType>(type, ignoreCase: true, out var parsed) && e.Type == parsed))
                .Where(e => tag is null || e.EntryTags.Any(t => string.Equals(t.Tag.Name, tag, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(e => e.UpdatedAt)
                .Select(EntryMapper.ToResponse)
                .ToArray();

            return Results.Ok(responses);
        }

        var query = db.ContextEntries
            .Include(e => e.EntryTags).ThenInclude(t => t.Tag)
            .Include(e => e.SkillFiles)
            .AsSplitQuery()
            .Where(e => e.ProjectId == projectId);

        if (!string.IsNullOrWhiteSpace(type) &&
            Enum.TryParse<EntryType>(type, ignoreCase: true, out var entryType))
        {
            query = query.Where(e => e.Type == entryType);
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            query = query.Where(e => e.EntryTags.Any(t => t.Tag.Name == tag));
        }

        var listed = await query
            .OrderByDescending(e => e.UpdatedAt)
            .ToListAsync(ct);

        return Results.Ok(listed.Select(EntryMapper.ToResponse).ToArray());
    }

    private static async Task<IResult> Create(
        CreateEntryRequest request,
        IValidator<CreateEntryRequest> validator,
        OvermindDbContext db,
        EntryIndexingService indexing,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var projectExists = await db.Projects.AnyAsync(p => p.Id == request.ProjectId, ct);
        if (!projectExists)
        {
            return HttpErrors.NotFound($"Project '{request.ProjectId}' was not found.");
        }

        var tags = await ResolveTagsAsync(db, request.Tags, ct);

        ContextEntry entry;
        if (request.Type == EntryType.Skill)
        {
            var files = request.SkillFiles!.Select(EntryMapper.ToEntity);
            entry = ContextEntry.CreateSkill(request.ProjectId, request.Title, request.Content, files);
        }
        else
        {
            entry = ContextEntry.Create(request.ProjectId, request.Type, request.Title, request.Content);
        }

        foreach (var tag in tags)
        {
            entry.AddTag(tag);
        }

        db.ContextEntries.Add(entry);
        await db.SaveChangesAsync(ct);

        await indexing.IndexAsync(entry, ct);

        return Results.Created($"/api/entries/{entry.Id}", EntryMapper.ToResponse(entry));
    }

    private static async Task<IResult> GetById(Guid id, OvermindDbContext db, CancellationToken ct)
    {
        var entry = await LoadEntryAsync(db, id, ct);
        return entry is null
            ? HttpErrors.NotFound($"Entry '{id}' was not found.")
            : Results.Ok(EntryMapper.ToResponse(entry));
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdateEntryRequest request,
        IValidator<UpdateEntryRequest> validator,
        OvermindDbContext db,
        EntryIndexingService indexing,
        BundledKitForkService kitFork,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var entry = await db.ContextEntries
            .Include(e => e.EntryTags).ThenInclude(t => t.Tag)
            .Include(e => e.SkillFiles)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (entry is null)
        {
            return HttpErrors.NotFound($"Entry '{id}' was not found.");
        }

        if (request.SkillFiles is { Length: > 0 } && entry.Type != EntryType.Skill)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid skill files",
                detail: "Only entries of type 'Skill' can carry skill files.");
        }

        var contentChanged = !string.Equals(entry.Content, request.Content, StringComparison.Ordinal);

        entry.UpdateContent(request.Content);
        db.Entry(entry).Property(e => e.Title).CurrentValue = request.Title.Trim();

        if (request.Tags is not null)
        {
            entry.EntryTags.Clear();
            foreach (var tag in await ResolveTagsAsync(db, request.Tags, ct))
            {
                entry.AddTag(tag);
            }
        }

        if (request.SkillFiles is { Length: > 0 })
        {
            db.SkillFiles.RemoveRange(entry.SkillFiles);
            entry.SkillFiles.Clear();

            foreach (var file in request.SkillFiles.Select(EntryMapper.ToEntity))
            {
                db.Entry(file).Property(f => f.EntryId).CurrentValue = entry.Id;
                db.SkillFiles.Add(file);
            }
        }

        if (contentChanged)
        {
            await kitFork.ForkExclusiveBundledOwnerAsync(entry.Id, ct);
        }

        await db.SaveChangesAsync(ct);

        await indexing.IndexAsync(entry, ct);

        return Results.Ok(EntryMapper.ToResponse(entry));
    }

    private static async Task<IResult> Delete(
        Guid id,
        OvermindDbContext db,
        EntryIndexingService indexing,
        CancellationToken ct)
    {
        var entry = await db.ContextEntries.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entry is null)
        {
            return HttpErrors.NotFound($"Entry '{id}' was not found.");
        }

        db.ContextEntries.Remove(entry);
        await db.SaveChangesAsync(ct);

        await indexing.RemoveAsync(id, ct);

        return Results.NoContent();
    }

    private static async Task<IResult> ListVersions(Guid id, OvermindDbContext db, CancellationToken ct)
    {
        var entryExists = await db.ContextEntries.AnyAsync(e => e.Id == id, ct);
        if (!entryExists)
        {
            return HttpErrors.NotFound($"Entry '{id}' was not found.");
        }

        var versions = await db.EntryVersions
            .Where(v => v.EntryId == id)
            .OrderBy(v => v.Version)
            .Select(v => new EntryVersionResponse(v.Version, v.Content, v.CreatedAt))
            .ToListAsync(ct);

        return Results.Ok(versions);
    }

    private static async Task<ContextEntry?> LoadEntryAsync(OvermindDbContext db, Guid id, CancellationToken ct)
    {
        return await db.ContextEntries
            .Include(e => e.EntryTags).ThenInclude(t => t.Tag)
            .Include(e => e.SkillFiles)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    private static async Task<List<Tag>> ResolveTagsAsync(OvermindDbContext db, string[]? names, CancellationToken ct)
    {
        var wanted = (names ?? [])
            .Select(name => name.Trim())
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (wanted.Length == 0)
        {
            return [];
        }

        var existing = await db.Tags
            .Where(t => wanted.Contains(t.Name))
            .ToListAsync(ct);

        foreach (var name in wanted)
        {
            if (existing.Any(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var created = Tag.Create(name);
            db.Tags.Add(created);
            existing.Add(created);
        }

        return existing;
    }
}
