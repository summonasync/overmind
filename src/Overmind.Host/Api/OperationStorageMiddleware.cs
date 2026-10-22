using Microsoft.Extensions.DependencyInjection;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public sealed class OperationStorageMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        if (context.Items.TryGetValue(OperationReplayFilter.PendingItemKey, out var value)
            && value is OperationReplayFilter.PendingOperation pending
            && context.Response.StatusCode is >= 200 and < 300)
        {
            var store = context.RequestServices.GetRequiredService<PlanningOperationStore>();
            await store.SaveAsync(
                pending.ProjectId,
                pending.OperationId,
                pending.RequestHash,
                context.Response.StatusCode,
                pending.Capture.CapturedBody,
                CancellationToken.None);
        }
    }
}
