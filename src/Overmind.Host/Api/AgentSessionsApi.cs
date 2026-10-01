using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Host.Api;

public static class AgentSessionsApi
{
    public static RouteGroupBuilder MapAgentSessionsApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/agent");

        group.MapPost("/sessions", Register);
        group.MapPost("/sessions/{id:guid}/end", End);

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
            session = AgentSession.Create(request.Alias, request.Vendor);
        }
        catch (ArgumentException ex)
        {
            return HttpErrors.BadRequest(ex.Message);
        }

        db.AgentSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/agent/sessions/{session.Id}", ToResponse(session));
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

    private static AgentSessionResponse ToResponse(AgentSession session) =>
        new(session.Id, session.Alias, session.Vendor, session.StartedAt, session.LastSeenAt, session.EndedAt);
}
