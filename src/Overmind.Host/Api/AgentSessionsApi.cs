using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public static class AgentSessionsApi
{
    public static RouteGroupBuilder MapAgentSessionsApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/agent");

        group.MapPost("/sessions", Register);
        group.MapPost("/sessions/{id:guid}/role", SetRole);
        group.MapPost("/sessions/{id:guid}/identity", SetIdentity);
        group.MapPost("/sessions/{id:guid}/end", End);
        group.MapPost("/sessions/{id:guid}/touch", Touch);

        return group;
    }

    private static async Task<IResult> Register(
        RegisterAgentSessionRequest request, OvermindDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Alias) || string.IsNullOrWhiteSpace(request.Vendor))
        {
            return HttpErrors.BadRequest("Alias and vendor are required to register an agent session.");
        }

        var alias = request.Alias.Trim();
        if (alias.Equals(AgentSession.PlaceholderAlias, StringComparison.Ordinal) &&
            request.PriorSessionId is { } priorId &&
            await db.AgentSessions.FirstOrDefaultAsync(s => s.Id == priorId, ct) is { } prior &&
            !prior.Alias.Equals(AgentSession.PlaceholderAlias, StringComparison.Ordinal))
        {
            // Reconnect re-attach: the workspace stamp names the row this adapter registered last
            // run, so resuming keeps its alias+vendor and its session id — the id claim locks match
            // on. Only the placeholder path resumes; an explicit alias is the operator's choice and
            // keeps the fresh-registration path, as does an alive rival holding the row's alias.
            var aliveSince = await AliveSinceAsync(db, ct);
            if (await FindAliveAliasHolderAsync(db, prior.Id, prior.Alias, aliveSince, ct) is null)
            {
                prior.Resume();
                await db.SaveChangesAsync(ct);
                return Results.Created($"/api/agent/sessions/{prior.Id}", ToResponse(prior));
            }
        }

        AgentSession session;
        try
        {
            session = AgentSession.Create(request.Alias, request.Vendor, request.Role ?? AgentSession.DefaultRole);
        }
        catch (ArgumentException ex)
        {
            return HttpErrors.BadRequest(ex.Message);
        }

        db.AgentSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/agent/sessions/{session.Id}", ToResponse(session));
    }

    private static async Task<IResult> SetRole(
        Guid id, SetAgentSessionRoleRequest request, OvermindDbContext db, CancellationToken ct)
    {
        var role = request.Role?.Trim();
        if (role is null ||
            (!role.Equals(AgentSession.DefaultRole, StringComparison.Ordinal) &&
             !role.Equals(AgentSession.MarshalRole, StringComparison.Ordinal) &&
             !role.Equals(AgentSession.EmbeddedRole, StringComparison.Ordinal)))
        {
            return HttpErrors.BadRequest(
                $"Role must be '{AgentSession.DefaultRole}', '{AgentSession.MarshalRole}' or '{AgentSession.EmbeddedRole}'.");
        }

        var session = await db.AgentSessions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (session is null)
        {
            return HttpErrors.NotFound($"Session '{id}' was not found.");
        }

        session.SetRole(role);
        await db.SaveChangesAsync(ct);
        return Results.Ok(ToResponse(session));
    }

    private static async Task<IResult> SetIdentity(
        Guid id, SetAgentSessionIdentityRequest request, OvermindDbContext db,
        FeatureClaimService claims, CancellationToken ct)
    {
        var session = await db.AgentSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (session is null)
        {
            return HttpErrors.NotFound($"Session '{id}' was not found.");
        }

        var alias = request.Alias?.Trim();
        var vendor = request.Vendor?.Trim();
        var aliveSince = await AliveSinceAsync(db, ct);

        var holder = await FindAliveAliasHolderAsync(db, id, alias, aliveSince, ct);
        if (holder is not null)
        {
            return HttpErrors.Conflict(AliasHeldDetail(alias, holder));
        }

        var identityChanged = !string.Equals(alias, session.Alias, StringComparison.Ordinal) ||
                              !string.Equals(vendor, session.Vendor, StringComparison.Ordinal);

        try
        {
            session.SetIdentity(alias!, vendor!);
        }
        catch (ArgumentException ex)
        {
            return HttpErrors.BadRequest(ex.Message);
        }

        // The pre-check above is only the friendly answer; between it and this write another adapter can
        // land the same alias, so the claim itself carries the guard — SQLite runs the statement as one
        // atomic write, and the loser sees the winner's row and gets 0 rows back.
        if (!await TryClaimAliasAsync(db, id, session.Alias, session.Vendor, aliveSince, ct))
        {
            var rival = await FindAliveAliasHolderAsync(db, id, session.Alias, aliveSince, ct);
            return HttpErrors.Conflict(rival is not null
                ? AliasHeldDetail(session.Alias, rival)
                : $"Alias '{session.Alias}' was claimed while this request was running; set the identity again.");
        }

        IReadOnlyList<string> moved = identityChanged
            ? await claims.ReattributeSessionClaimsAsync(id, session.Alias, session.Vendor, ct)
            : [];

        return Results.Ok(new AgentSessionIdentityResponse(
            ToResponse(session), BuildIdentityWarnings(session.Alias, session.Vendor, moved)));
    }

    // The union of project alive windows: a holder counts as alive while any project would still count it.
    private static async Task<DateTime> AliveSinceAsync(OvermindDbContext db, CancellationToken ct)
    {
        var threshold = await db.Projects
            .Select(p => (int?)p.ClaimStaleThresholdMinutes)
            .MaxAsync(ct) ?? Project.DefaultClaimStaleThresholdMinutes;

        return threshold > 0 ? DateTime.UtcNow.AddMinutes(-threshold) : DateTime.MinValue;
    }

    private static async Task<AgentSession?> FindAliveAliasHolderAsync(
        OvermindDbContext db, Guid sessionId, string? alias, DateTime aliveSince, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(alias))
        {
            return null;
        }

        return await db.AgentSessions
            .Where(s => s.Id != sessionId &&
                        s.Alias == alias &&
                        s.EndedAt == null &&
                        s.LastSeenAt >= aliveSince)
            .FirstOrDefaultAsync(ct);
    }

    private static async Task<bool> TryClaimAliasAsync(
        OvermindDbContext db, Guid sessionId, string alias, string vendor,
        DateTime aliveSince, CancellationToken ct)
    {
        var claimed = await db.AgentSessions
            .Where(s => s.Id == sessionId &&
                        !db.AgentSessions.Any(h =>
                            h.Id != sessionId && h.Alias == alias && h.EndedAt == null && h.LastSeenAt >= aliveSince))
            .ExecuteUpdateAsync(update => update
                .SetProperty(s => s.Alias, alias)
                .SetProperty(s => s.Vendor, vendor), ct);

        return claimed == 1;
    }

    private static string AliasHeldDetail(string? alias, AgentSession holder) =>
        $"Alias '{alias}' is held by the alive session '{holder.Alias}' by '{holder.Vendor}'; " +
        "a stale or ended holder frees the alias.";

    private static IReadOnlyList<string> BuildIdentityWarnings(
        string alias, string vendor, IReadOnlyList<string> movedSlugs)
    {
        List<string> warnings = [];

        if (alias.Equals(AgentIdentityDefaults.Alias, StringComparison.Ordinal) &&
            vendor.Equals(AgentIdentityDefaults.Vendor, StringComparison.Ordinal))
        {
            warnings.Add(
                $"The placeholder identity '{AgentIdentityDefaults.Alias}' by '{AgentIdentityDefaults.Vendor}' was set; " +
                "invent a project alias and set it again before claiming a feature or posting to wave chat.");
        }

        if (movedSlugs.Count > 0)
        {
            warnings.Add(
                $"Identity changed while holding claims: {string.Join(", ", movedSlugs)}; " +
                $"the claim snapshots now read '{alias}' by '{vendor}' and each feature logged the move.");
        }

        return warnings;
    }

    private static async Task<IResult> End(Guid id, OvermindDbContext db, CancellationToken ct)
    {
        var session = await db.AgentSessions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (session is null)
        {
            return HttpErrors.NotFound($"Session '{id}' was not found.");
        }

        session.End();
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> Touch(Guid id, OvermindDbContext db, CancellationToken ct)
    {
        var session = await db.AgentSessions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (session is null)
        {
            return HttpErrors.NotFound($"Session '{id}' was not found.");
        }

        session.Touch();
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static AgentSessionResponse ToResponse(AgentSession session) =>
        new(session.Id, session.Alias, session.Vendor, session.StartedAt, session.LastSeenAt, session.EndedAt);
}
