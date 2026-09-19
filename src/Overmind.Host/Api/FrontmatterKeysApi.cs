using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public static class FrontmatterKeysApi
{
    public static RouteGroupBuilder MapFrontmatterKeysApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");
        group.MapGet("/frontmatter-keys", ListKeys);
        return group;
    }

    private static async Task<IResult> ListKeys(FrontmatterKeyRegistry registry, CancellationToken ct)
    {
        var keys = await registry.ListAsync(ct);

        return Results.Ok(keys.Select(k => new FrontmatterKeyResponse(k.Name, k.Color)).ToList());
    }
}
