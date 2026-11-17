using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Mapping;
using Overmind.Host.Api.Validation;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Kits;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
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
        group.MapPost("/entries/{id:guid}/lift", Lift);
        group.MapPost("/entries/{id:guid}/append", Append);
        group.MapGet("/entries/{id:guid}/versions", ListVersions);
        group.MapPost("/projects/{projectId:guid}/entries/{entryId:guid}/ack", AckForHuman);

        var agent = routes.MapGroup("/api/agent");
        agent.MapPost("/projects/{projectId:guid}/entries/{entryId:guid}/ack", AckForAgent);

        // Spec R6 as an invariant rather than a list: every entry mutation registered under the /api/agent prefix
        // lands on this subgroup, and the journal refusal hangs on the subgroup, so a new verb inherits it by
        // placement.
        // The route-table test derives its cases from the registered endpoints, so a mutation verb registered
        // anywhere else under this prefix turns that test red without anyone updating a verb list.
        var agentEntryWrites = routes.MapGroup("/api/agent").AddEndpointFilter(JournalWriteLockFilter.Instance);
        agentEntryWrites.MapPost("/entries", Create);
        agentEntryWrites.MapPut("/entries/{id:guid}", Update);
        agentEntryWrites.MapDelete("/entries/{id:guid}", Delete);
        agentEntryWrites.MapPost("/entries/{id:guid}/lift", Lift);
        agentEntryWrites.MapPost("/entries/{id:guid}/append", Append);

        return group;
    }

    private static async Task<IResult> ListByProject(
        Guid projectId,
        string? type,
        string? tag,
        string? state,
        DateTime? since,
        DateTime? until,
        string? author,
        string? q,
        string? link,
        string? project,
        OvermindDbContext db,
        FtsSearchService search,
        EntryLinkTriage triage,
        CancellationToken ct)
    {
        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            return HttpErrors.NotFound($"Project '{projectId}' was not found.");
        }

        var stateFailure = EntryStateFilter.Validate(state, type, out var stateFilter);
        if (stateFailure is not null)
        {
            return stateFailure;
        }

        var linkFailure = EntryStreamFilter.ValidateLink(link, out var linked);
        if (linkFailure is not null)
        {
            return linkFailure;
        }

        var (projectFilter, projectFailure) = await EntryStreamFilter.ResolveProjectAsync(db, project, ct);
        if (projectFailure is not null)
        {
            return projectFailure;
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var hits = await search.SearchAsync(projectId, q, 100, ct);
            var hitIds = hits.Select(h => h.EntryId).ToArray();

            var searched = db.ContextEntries
                .Include(e => e.EntryTags).ThenInclude(t => t.Tag)
                .Include(e => e.SkillFiles)
                .Include(e => e.Acks)
                .AsSplitQuery()
                .Where(e => hitIds.Contains(e.Id));

            var entries = await EntryStreamFilter.Apply(searched, triage, linked, projectFilter)
                .ToListAsync(ct);

            var responses = entries
                .Where(e => type is null || (Enum.TryParse<EntryType>(type, ignoreCase: true, out var parsed) && e.Type == parsed))
                .Where(e => stateFilter == EntryStateFilter.All || e.State == stateFilter)
                .Where(e => tag is null || e.EntryTags.Any(t => string.Equals(t.Tag.Name, tag, StringComparison.OrdinalIgnoreCase)))
                .Where(e => EntryQueryFilters.Matches(e, since, until, author))
                .OrderByDescending(e => e.UpdatedAt)
                .Select(EntryMapper.ToResponse)
                .ToArray();

            return Results.Ok(responses);
        }

        var query = db.ContextEntries
            .Include(e => e.EntryTags).ThenInclude(t => t.Tag)
            .Include(e => e.SkillFiles)
            .Include(e => e.Acks)
            .AsSplitQuery()
            .Where(e => e.ProjectId == projectId);

        if (!string.IsNullOrWhiteSpace(type) &&
            Enum.TryParse<EntryType>(type, ignoreCase: true, out var entryType))
        {
            query = query.Where(e => e.Type == entryType);
        }

        if (stateFilter != EntryStateFilter.All)
        {
            query = query.Where(e => e.State == stateFilter);
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            query = query.Where(e => e.EntryTags.Any(t => t.Tag.Name == tag));
        }

        query = EntryQueryFilters.Apply(query, since, until, author);
        query = EntryStreamFilter.Apply(query, triage, linked, projectFilter);

        var listed = await query
            .OrderByDescending(e => e.UpdatedAt)
            .ToListAsync(ct);

        return Results.Ok(listed.Select(EntryMapper.ToResponse).ToArray());
    }

    private static async Task<IResult> Create(
        CreateEntryRequest request,
        IValidator<CreateEntryRequest> validator,
        HttpRequest http,
        OvermindDbContext db,
        EntryIndexingService indexing,
        EntryLinkWriter writer,
        JournalProjectStore journals,
        ILoggerFactory writes,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var explicitProject = request.ProjectId is { } named && named != Guid.Empty ? named : (Guid?)null;
        if (explicitProject is { } target)
        {
            var projectExists = await db.Projects.AnyAsync(p => p.Id == target, ct);
            if (!projectExists)
            {
                return HttpErrors.NotFound($"Project '{target}' was not found.");
            }
        }

        ContextEntry? committed = null;
        var outcome = await EntryWriteCommit.RunAsync(
            db,
            writer,
            writes,
            async attemptCt =>
            {
                // Spec R2: the reserved container is born inside this same transaction, so the human door never
                // needs an id the browser cannot know. A named project still wins over it.
                var containerId = explicitProject ?? await journals.EnsureIdAsync(attemptCt);

                var tags = await ResolveTagsAsync(db, request.Tags, attemptCt);

                ContextEntry entry;
                if (request.Type == EntryType.Skill)
                {
                    var files = request.SkillFiles!.Select(EntryMapper.ToEntity);
                    entry = ContextEntry.CreateSkill(containerId, request.Title, request.Content, files);
                }
                else
                {
                    entry = ContextEntry.Create(containerId, request.Type, request.Title, request.Content);
                }

                foreach (var tag in tags)
                {
                    entry.AddTag(tag);
                }

                db.ContextEntries.Add(entry);
                if (request.Enforces.HasValue)
                {
                    db.Entry(entry).Property(e => e.Enforces).CurrentValue = request.Enforces.Value;
                }

                var authorAlias = await ResolveAuthorAliasAsync(db, http, attemptCt);
                if (authorAlias is not null)
                {
                    db.Entry(entry).Property(e => e.AuthorAlias).CurrentValue = authorAlias;
                }

                committed = entry;
                return new EntryWriteCommit.Work(
                    entry.Id,
                    entry.Content,
                    async saveCt =>
                    {
                        await db.SaveChangesAsync(saveCt);
                        return Results.Created($"/api/entries/{entry.Id}", EntryMapper.ToResponse(entry));
                    });
            },
            ct);

        if (committed is not null)
        {
            await indexing.IndexAsync(committed, ct);
        }

        return outcome;
    }

    private static async Task<IResult> GetById(
        Guid id,
        OvermindDbContext db,
        EntryLinkResolver resolver,
        EntryLinkReader links,
        CancellationToken ct)
    {
        var entry = await LoadEntryAsync(db, id, ct);
        if (entry is null)
        {
            return HttpErrors.NotFound($"Entry '{id}' was not found.");
        }

        var view = await EntryLinkProjection.LoadAsync(resolver, links, entry, ct);
        return Results.Ok(EntryMapper.ToDetailResponse(entry, view));
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdateEntryRequest request,
        IValidator<UpdateEntryRequest> validator,
        OvermindDbContext db,
        EntryIndexingService indexing,
        BundledKitForkService kitFork,
        EntryLinkWriter writer,
        ILoggerFactory writes,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        ContextEntry? committed = null;
        var outcome = await EntryWriteCommit.RunAsync(
            db,
            writer,
            writes,
            async attemptCt =>
            {
                var entry = await db.ContextEntries
                    .Include(e => e.EntryTags).ThenInclude(t => t.Tag)
                    .Include(e => e.SkillFiles)
                    .Include(e => e.Acks)
                    .FirstOrDefaultAsync(e => e.Id == id, attemptCt);

                if (entry is null)
                {
                    return EntryWriteCommit.Work.Rejected(HttpErrors.NotFound($"Entry '{id}' was not found."));
                }

                if (request.SkillFiles is { Length: > 0 } && entry.Type != EntryType.Skill)
                {
                    return EntryWriteCommit.Work.Rejected(Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Invalid skill files",
                        detail: "Only entries of type 'Skill' can carry skill files."));
                }

                if (request.Enforces is { } enforces)
                {
                    if (enforces && entry.Type != EntryType.Decision)
                    {
                        return EntryWriteCommit.Work.Rejected(Results.Problem(
                            statusCode: StatusCodes.Status400BadRequest,
                            title: "Invalid enforces flag",
                            detail: CreateEntryRequestValidator.EnforcesDecisionOnly));
                    }

                    db.Entry(entry).Property(e => e.Enforces).CurrentValue = enforces;
                }

                var contentChanged = !string.Equals(entry.Content, request.Content, StringComparison.Ordinal);

                entry.UpdateContent(request.Content);
                db.Entry(entry).Property(e => e.Title).CurrentValue = request.Title.Trim();

                if (request.Tags is not null)
                {
                    entry.EntryTags.Clear();
                    foreach (var tag in await ResolveTagsAsync(db, request.Tags, attemptCt))
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
                    await kitFork.ForkExclusiveBundledOwnerAsync(entry.Id, attemptCt);
                }

                committed = entry;
                return new EntryWriteCommit.Work(
                    entry.Id,
                    entry.Content,
                    async saveCt =>
                    {
                        await db.SaveChangesAsync(saveCt);
                        return Results.Ok(EntryMapper.ToResponse(entry));
                    });
            },
            ct);

        if (committed is not null)
        {
            await indexing.IndexAsync(committed, ct);
        }

        return outcome;
    }

    private static async Task<IResult> Delete(
        Guid id,
        string? reason,
        HttpRequest http,
        OvermindDbContext db,
        EntryIndexingService indexing,
        CancellationToken ct)
    {
        var entry = await db.ContextEntries.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entry is null)
        {
            return HttpErrors.NotFound($"Entry '{id}' was not found.");
        }

        ContextEntry? tombstone = null;
        if (entry.Type != EntryType.Tombstone)
        {
            tombstone = ContextEntry.CreateTombstone(
                entry,
                ResolveDoor(http),
                await ResolveActorAsync(db, http, ct),
                reason);
            db.ContextEntries.Add(tombstone);
        }

        db.ContextEntries.Remove(entry);
        await db.SaveChangesAsync(ct);

        await indexing.RemoveAsync(id, ct);
        if (tombstone is not null)
        {
            await indexing.IndexAsync(tombstone, ct);
        }

        return tombstone is null
            ? Results.NoContent()
            : Results.Ok(EntryMapper.ToResponse(tombstone));
    }

    private static PlanningLogSource ResolveDoor(HttpRequest http) =>
        SessionHeader.TryRead(http, out _)
            ? PlanningLogSource.Agent
            : PlanningLogSource.Ui;

    private static async Task<IResult> Lift(
        Guid id,
        EntryLiftRequest request,
        IValidator<EntryLiftRequest> validator,
        HttpRequest http,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var entry = await LoadEntryAsync(db, id, ct);
        if (entry is null)
        {
            return HttpErrors.NotFound($"Entry '{id}' was not found.");
        }

        if (request.SupersededById is { } supersededById)
        {
            var targetFailure = await ValidateSupersedeTargetAsync(db, entry, supersededById, ct);
            if (targetFailure is not null)
            {
                return targetFailure;
            }
        }

        try
        {
            entry.Lift(request.Reason, await ResolveActorAsync(db, http, ct), request.Proof, request.SupersededById);
        }
        catch (InvalidOperationException ex)
        {
            return HttpErrors.BadRequest(ex.Message);
        }

        await db.SaveChangesAsync(ct);

        return Results.Ok(EntryMapper.ToResponse(entry));
    }

    private static async Task<IResult?> ValidateSupersedeTargetAsync(
        OvermindDbContext db,
        ContextEntry entry,
        Guid supersededById,
        CancellationToken ct)
    {
        if (supersededById == entry.Id)
        {
            return HttpErrors.BadRequest($"Entry '{entry.Id}' cannot supersede itself.");
        }

        var target = await db.ContextEntries.FirstOrDefaultAsync(e => e.Id == supersededById, ct);
        if (target is null)
        {
            return HttpErrors.BadRequest($"Superseding entry '{supersededById}' was not found.");
        }

        if (target.ProjectId != entry.ProjectId)
        {
            return HttpErrors.BadRequest($"Superseding entry '{supersededById}' belongs to a different project.");
        }

        if (target.Type == EntryType.Tombstone)
        {
            return HttpErrors.BadRequest($"Superseding entry '{supersededById}' is a tombstone.");
        }

        return null;
    }

    private static async Task<IResult> Append(
        Guid id,
        EntryAppendRequest request,
        IValidator<EntryAppendRequest> validator,
        OvermindDbContext db,
        EntryIndexingService indexing,
        BundledKitForkService kitFork,
        EntryLinkWriter writer,
        ILoggerFactory writes,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        ContextEntry? committed = null;
        var outcome = await EntryWriteCommit.RunAsync(
            db,
            writer,
            writes,
            async attemptCt =>
            {
                var entry = await LoadEntryAsync(db, id, attemptCt);
                if (entry is null)
                {
                    return EntryWriteCommit.Work.Rejected(HttpErrors.NotFound($"Entry '{id}' was not found."));
                }

                entry.AppendFollowUp(request.Text);
                await kitFork.ForkExclusiveBundledOwnerAsync(entry.Id, attemptCt);

                committed = entry;
                return new EntryWriteCommit.Work(
                    entry.Id,
                    entry.Content,
                    async saveCt =>
                    {
                        await db.SaveChangesAsync(saveCt);
                        return Results.Ok(EntryMapper.ToResponse(entry));
                    });
            },
            ct);

        if (committed is not null)
        {
            await indexing.IndexAsync(committed, ct);
        }

        return outcome;
    }

    private static async Task<string?> ResolveAuthorAliasAsync(OvermindDbContext db, HttpRequest http, CancellationToken ct)
    {
        if (!SessionHeader.TryRead(http, out var sessionId))
        {
            return null;
        }

        var session = await db.AgentSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        return session?.Alias ?? sessionId.ToString();
    }

    private static async Task<string?> ResolveActorAsync(OvermindDbContext db, HttpRequest http, CancellationToken ct)
    {
        if (!SessionHeader.TryRead(http, out var sessionId))
        {
            return null;
        }

        var session = await db.AgentSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        return session is null
            ? sessionId.ToString()
            : $"{session.Alias} ({session.Vendor})";
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
            .Include(e => e.Acks)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    internal static async Task<IResult> AckForAgent(
        Guid projectId,
        Guid entryId,
        EntryAckService acks,
        OvermindDbContext db,
        HttpRequest http,
        CancellationToken ct)
    {
        if (!SessionHeader.TryRead(http, out var sessionId))
        {
            return AgentIdentityRequired();
        }

        var session = await db.AgentSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null)
        {
            return AgentIdentityRequired();
        }

        return await AckAsync(
            projectId, entryId, new DirectiveAuthor(session.Alias, session.Vendor, session.Id), acks, ct);
    }

    internal static async Task<IResult> AckForHuman(
        Guid projectId,
        Guid entryId,
        EntryAckService acks,
        IValidator<EntryAckHumanRequest> validator,
        [FromBody] EntryAckHumanRequest? request,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request ?? new EntryAckHumanRequest(null, null), ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        return await AckAsync(projectId, entryId, new DirectiveAuthor(request?.Alias, request?.Vendor, null), acks, ct);
    }

    private static async Task<IResult> AckAsync(
        Guid projectId,
        Guid entryId,
        DirectiveAuthor author,
        EntryAckService acks,
        CancellationToken ct)
    {
        try
        {
            var outcome = await acks.AckAsync(projectId, entryId, author, ct);
            return Results.Ok(new EntryAckResponse(
                outcome.Ack.ContextEntryId,
                outcome.AckedNow,
                outcome.Ack.Alias,
                outcome.Ack.Vendor,
                outcome.Ack.At));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    private static IResult AgentIdentityRequired() =>
        HttpErrors.BadRequest(
            $"Entry acknowledgement requires the {SessionHeader.Name} header and a registered agent session — " +
            "register via POST /api/agent/sessions.");

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
