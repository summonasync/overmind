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
        var session = await db.AgentSessions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (session is null)
        {
            return HttpErrors.NotFound($"Session '{id}' was not found.");
        }

        var alias = request.Alias?.Trim();
        var vendor = request.Vendor?.Trim();

        var holder = await FindAliveAliasHolderAsync(db, id, alias, ct);
        if (holder is not null)
        {
            return HttpErrors.Conflict(
                $"Alias '{alias}' is held by the alive session '{holder.Alias}' by '{holder.Vendor}'; " +
                "a stale or ended holder frees the alias.");
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

        IReadOnlyList<string> moved = identityChanged
            ? await claims.ReattributeSessionClaimsAsync(id, alias!, vendor!, ct)
            : [];

        await db.SaveChangesAsync(ct);
        return Results.Ok(new AgentSessionIdentityResponse(
            ToResponse(session), BuildIdentityWarnings(alias!, vendor!, moved)));
    }

    private static async Task<AgentSession?> FindAliveAliasHolderAsync(
        OvermindDbContext db, Guid sessionId, string? alias, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(alias))
        {
            return null;
        }

        // The union of project alive windows: a holder counts as alive while any project would still count it.
        var threshold = await db.Projects
            .Select(p => (int?)p.ClaimStaleThresholdMinutes)
            .MaxAsync(ct) ?? Project.DefaultClaimStaleThresholdMinutes;

        var candidates = await db.AgentSessions
            .Where(s => s.Id != sessionId && s.Alias == alias)
            .ToListAsync(ct);

        return candidates.FirstOrDefault(s =>
            string.Equals(s.Alias, alias, StringComparison.Ordinal) &&
            s.Liveness(DateTime.UtcNow, threshold) == AgentSessionLiveness.Alive);
    }

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
