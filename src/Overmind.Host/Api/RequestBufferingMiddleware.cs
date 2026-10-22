namespace Overmind.Host.Api;

public sealed class RequestBufferingMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var mutating = !HttpMethods.IsGet(context.Request.Method)
                       && !HttpMethods.IsHead(context.Request.Method);
        if (mutating
            && context.Request.Path.StartsWithSegments("/api/agent", StringComparison.OrdinalIgnoreCase))
        {
            context.Request.EnableBuffering();
        }

        return next(context);
    }
}
