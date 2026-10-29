using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Wake;

namespace Overmind.Host.Api;

public static class AgentWakeApi
{
    public const string Description =
        "Pending signals for the calling session, side-effect free. Reply signals are human replies " +
        "on this session's notifications with RepliedAt after since, defaulting to the last 7 days; " +
        "mention signals are wave chat messages mentioning this session's alias with CreatedAt after " +
        "since, defaulting to after the session's LastSeenAt before this request touched it. Signals are " +
        "ordered by createdAt asc, capped at 100; the cursor is the newest createdAt seen followed by '|' " +
        "and the newest signal's ref id, or the request since on an empty page — echo it verbatim as since to page on.";

    public static RouteGroupBuilder MapAgentWakeApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/agent");
        group.MapGet("/wake", Wake).WithDescription(Description);

        return group;
    }

    private static async Task<IResult> Wake(
        [FromQuery] string? since,
        AgentWakeService wake,
        OvermindDbContext db,
        HttpContext context,
        CancellationToken ct)
    {
        if (!SessionHeader.TryRead(context.Request, out var sessionId))
        {
            return AgentIdentityRequired();
        }

        if (!WakeCursor.TryDecode(since, out var sinceAt, out var sinceRefId))
        {
            return HttpErrors.BadRequest(
                "The since parameter must be an ISO 8601 timestamp, optionally followed by '|' " +
                "and the cursor ref id echoed from the previous page.");
        }

        var session = await db.AgentSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null)
        {
            return AgentIdentityRequired();
        }

        var mentionSince = context.Items.TryGetValue(SessionTouchMiddleware.PreTouchLastSeenKey, out var touched)
            && touched is DateTime beforeTouch
                ? beforeTouch
                : session.LastSeenAt;

        var page = await wake.PendingAsync(session.Alias, mentionSince, sinceAt, sinceRefId, ct);
        return Results.Ok(new AgentWakeResponse(
        [
            .. page.Signals.Select(ToResponse)
        ], WakeCursor.Encode(page.Cursor, page.CursorRefId)));
    }

    private static AgentWakeSignalResponse ToResponse(AgentWakeSignal signal) =>
        new(signal.Kind, signal.RefId, signal.RoomId, signal.From, signal.CreatedAt, signal.Excerpt);

    private static IResult AgentIdentityRequired() =>
        HttpErrors.BadRequest(
            $"Wake polling requires the {SessionHeader.Name} header and a registered agent session — " +
            "register via POST /api/agent/sessions.");
}
