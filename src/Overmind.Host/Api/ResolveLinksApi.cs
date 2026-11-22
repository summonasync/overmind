using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Validation;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Host.Api;

public static class ResolveLinksApi
{
    internal const int ContentLimit = SoftCapLimits.FollowUpText;

    public static RouteGroupBuilder MapResolveLinksApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");
        group.MapPost("/entries/resolve-links", Resolve);
        return group;
    }

    private static async Task<IResult> Resolve(
        EntryResolveLinksRequest request,
        IValidator<EntryResolveLinksRequest> validator,
        OvermindDbContext db,
        EntryLinkResolver resolver,
        EntryLinkReader links,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        if (request.Content.Length > SoftCap.Ceiling(ContentLimit))
        {
            return HttpErrors.BadRequest(
                $"Content exceeds the {ContentLimit} character soft cap for a resolve preview; nothing was resolved.",
                fix: $"Trim the content to {ContentLimit} characters or fewer and retry.");
        }

        if (request.EntryId is { } anchor &&
            !await db.ContextEntries.AnyAsync(e => e.Id == anchor, ct))
        {
            return HttpErrors.NotFound($"Entry '{anchor}' was not found.");
        }

        var view = await EntryLinkProjection.ResolveProposedAsync(
            resolver, links, request.Content, request.EntryId, ct);
        return Results.Ok(view);
    }
}
