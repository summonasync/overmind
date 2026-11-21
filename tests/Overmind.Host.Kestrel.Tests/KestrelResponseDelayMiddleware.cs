using Microsoft.AspNetCore.Http;

namespace Overmind.Host.Kestrel.Tests;

public sealed class KestrelResponseDelayMiddleware(RequestDelegate next, KestrelResponseDelayGate gate)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (gate.TryTakeMatching(context.Request.Path.Value, out var slot))
        {
            if (slot.DelayAfterHandler)
            {
                await next(context);
                await gate.HoldAsync(context, slot);
                return;
            }

            await gate.HoldAsync(context, slot);
        }

        await next(context);
    }
}
