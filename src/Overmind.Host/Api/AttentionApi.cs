using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Mapping;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public static class AttentionApi
{
    public static RouteGroupBuilder MapAttentionApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");
        group.MapGet("/attention/ceremonies", Ceremonies);

        return group;
    }

    internal static async Task<IResult> Ceremonies(
        CeremoniesService service,
        BrowserOrigin origin,
        HttpContext context,
        CancellationToken ct)
    {
        var features = await service.ListAsync(ct);

        return Results.Ok(new CeremoniesResponse(features
            .Select(f => new CeremonyResponse(
                f.ProjectId,
                f.ProjectName,
                f.FeatureSlug,
                PlanningMapper.FeatureNumber(f.FeatureSlug),
                f.Title,
                f.Reasons,
                NotificationLinks.Dashboard(origin.Resolve(context), f.ProjectId, f.FeatureSlug, null)))
            .ToArray()));
    }
}
