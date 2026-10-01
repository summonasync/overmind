using Microsoft.EntityFrameworkCore;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Host.Api;

public sealed class SessionTouchMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, OvermindDbContext db)
    {
        if (SessionHeader.TryRead(context.Request, out var sessionId))
        {
            var session = await db.AgentSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId, context.RequestAborted);
            if (session is not null)
            {
                session.Touch();
                await db.SaveChangesAsync(context.RequestAborted);
            }
        }

        await next(context);
    }
}
