using Microsoft.EntityFrameworkCore;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Notifications;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public static class HumanInboxApi
{
    public static RouteGroupBuilder MapHumanInboxApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");
        group.MapGet("/human/inbox", List);
        return group;
    }

    internal static async Task<IResult> List(
        Guid projectId, OvermindDbContext db, HumanInboxService inbox, BrowserOrigin origin, CancellationToken ct)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == projectId, ct))
        {
            return HttpErrors.NotFound($"Project '{projectId}' was not found.");
        }

        var entries = await inbox.ListAsync(projectId, ct);
        var bound = origin.BoundOrigin;

        return Results.Ok(new HumanInboxResponse(projectId,
            [.. entries.Select(e => ToItem(e, bound is null
                ? null
                : NotificationLinks.Dashboard(bound, projectId, e.Slug, "result")))]));
    }

    private static HumanInboxItemResponse ToItem(HumanInboxEntry entry, string? link) =>
        new(entry.Slug,
            entry.Title,
            entry.Phase,
            entry.QueuePosition,
            entry.ClaimedByAlias,
            entry.ClaimState,
            entry.PendingSince,
            link);
}
