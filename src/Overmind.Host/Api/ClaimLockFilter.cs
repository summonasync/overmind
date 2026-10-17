using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public sealed class ClaimLockFilter : IEndpointFilter
{
    public static readonly ClaimLockFilter Instance = new();

    private ClaimLockFilter()
    {
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (http.GetEndpoint()?.Metadata.GetMetadata<OpenCoordinationSurface>() is not null)
        {
            return await next(context);
        }

        if (SessionHeader.TryRead(http.Request, out var sessionId))
        {
            var route = http.Request.RouteValues;
            var claims = http.RequestServices.GetRequiredService<FeatureClaimService>();
            try
            {
                if (route["id"] is { } rawArtifact && Guid.TryParse(rawArtifact.ToString(), out var artifactId))
                {
                    await claims.EnsureHolderForArtifactAsync(artifactId, sessionId, http.RequestAborted);
                }
                else if (Guid.TryParse(route["projectId"]?.ToString(), out var projectId)
                         && route["slug"] is { } rawSlug)
                {
                    await claims.EnsureHolderAsync(projectId, rawSlug.ToString()!, sessionId, http.RequestAborted);
                }
            }
            catch (PlanningException ex)
            {
                return PlanningErrors.Map(ex);
            }
        }

        return await next(context);
    }
}
