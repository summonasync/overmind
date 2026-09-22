namespace Overmind.Host.Api;

public static class ShutdownApi
{
    public static RouteGroupBuilder MapShutdownApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");
        group.MapPost("/shutdown", Shutdown);
        return group;
    }

    private static IResult Shutdown(HttpContext context, IHostApplicationLifetime lifetime)
    {
        context.Response.OnCompleted(() =>
        {
            lifetime.StopApplication();
            return Task.CompletedTask;
        });

        return Results.Accepted();
    }
}
