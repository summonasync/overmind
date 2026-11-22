using Microsoft.EntityFrameworkCore;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Host.Api;

public sealed class SessionTouchMiddleware(RequestDelegate next)
{
    public const string PreTouchLastSeenKey = "Overmind:PreTouchLastSeenAt";

    // Gates that run after this middleware need the caller's pre-touch liveness; the stored
    // LastSeenAt is already freshened by the time they read it.
    public static DateTime? PreTouchLastSeenOf(HttpRequest request) =>
        request.HttpContext.Items.TryGetValue(PreTouchLastSeenKey, out var seen) && seen is DateTime stamp
            ? stamp
            : null;

    public async Task InvokeAsync(HttpContext context, OvermindDbContext db)
    {
        if (SessionHeader.TryRead(context.Request, out var sessionId))
        {
            var session = await db.AgentSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId, context.RequestAborted);
            if (session is not null)
            {
                if (session.EndedAt is null)
                {
                    context.Items[PreTouchLastSeenKey] = session.LastSeenAt;
                }

                session.Touch();
                await db.SaveChangesAsync(context.RequestAborted);
            }
        }

        await next(context);
    }
}
