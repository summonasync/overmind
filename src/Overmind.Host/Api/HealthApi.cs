using System.Reflection;
using Overmind.Core;
using Overmind.Host.Api.Contracts;

namespace Overmind.Host.Api;

public static class HealthApi
{
    public static RouteGroupBuilder MapHealthApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");
        group.MapGet("/health", GetHealth);
        return group;
    }

    private static async Task<IResult> GetHealth(ISemanticSearch semanticSearch, CancellationToken ct)
    {
        var vectorSearch = await semanticSearch.HealthReportAsync(ct);
        var version = Assembly.GetExecutingAssembly()
                         .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                     ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                     ?? "unknown";

        return Results.Ok(new HealthResponse("ok", vectorSearch, version));
    }
}
