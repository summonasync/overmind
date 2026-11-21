using FluentValidation;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Mapping;
using Overmind.Infrastructure.Notifications;

namespace Overmind.Host.Api;

public static class NotificationsApi
{
    private const int MaxTake = 500;

    public static RouteGroupBuilder MapNotificationsApi(this IEndpointRouteBuilder routes)
    {
        var human = routes.MapGroup("/api");
        human.MapGet("/notifications", List);
        human.MapPost("/notifications/{id:guid}/read", MarkRead);
        human.MapPost("/notifications/{id:guid}/reply", Reply);
        human.MapDelete("/notifications/{id:guid}", Delete);
        human.MapPost("/notifications/read-all", MarkAllRead);
        human.MapPost("/notifications/clear-read", ClearRead);
        human.MapPost("/notifications/clear-conversations", ClearConversations);

        var agent = routes.MapGroup("/api/agent");
        agent.MapPost("/notifications", Create);
        agent.MapGet("/notifications/replies", Replies);

        return human;
    }

    internal static async Task<IResult> Create(
        CreateNotificationRequest request,
        IValidator<CreateNotificationRequest> validator,
        NotificationsService service,
        BrowserOrigin origin,
        HttpContext context,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var kind = Enum.Parse<NotificationKind>(request.Kind, ignoreCase: true);
        var warnings = new List<object>();
        var text = SoftCapSoften.Cap(request.Text, AgentNotification.MaxTextLength, warnings);
        var notification = await service.CreateAsync(
            kind, text!, request.ProjectId, request.FeatureSlug, request.Tab, request.From, ct);

        if (notification is null)
        {
            return HttpErrors.NotFound($"Project '{request.ProjectId}' was not found.");
        }

        var url = NotificationLinks.Dashboard(
            origin.Resolve(context), notification.ProjectId, notification.FeatureSlug, notification.Tab);

        return Results.Created($"/api/notifications/{notification.Id}",
            NotificationMapper.ToAgentResponse(notification, url) with { Warnings = SoftCapSoften.Merge(warnings) });
    }

    internal static async Task<IResult> Replies(
        NotificationsService service,
        Guid? projectId,
        bool includeRead = true,
        DateTime? since = null,
        string? kind = null,
        string? featureSlug = null,
        CancellationToken ct = default)
    {
        NotificationKind? kindFilter = null;
        if (!string.IsNullOrWhiteSpace(kind))
        {
            var trimmed = kind.Trim();
            if (!Enum.TryParse<NotificationKind>(trimmed, ignoreCase: true, out var parsed)
                || !string.Equals(parsed.ToString(), trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return HttpErrors.BadRequest(
                    $"Invalid notification kind '{kind}'. Expected one of: info, question, approval.");
            }

            kindFilter = parsed;
        }

        var replies = await service.ListRepliesAsync(
            projectId,
            includeRead,
            since?.ToUniversalTime(),
            kindFilter,
            string.IsNullOrWhiteSpace(featureSlug) ? null : featureSlug.Trim(),
            ct);

        return Results.Ok(new RepliesResponse(replies.Select(NotificationMapper.ToResponse).ToArray()));
    }

    internal static async Task<IResult> List(
        NotificationsService service,
        bool includeHidden = false,
        bool awaiting = false,
        int take = NotificationsService.ListCapacity,
        CancellationToken ct = default)
    {
        if (take is < 1 or > MaxTake)
        {
            return HttpErrors.BadRequest($"take must be between 1 and {MaxTake}.");
        }

        var overview = await service.GetOverviewAsync(includeHidden, awaiting, take, ct);

        return Results.Ok(new NotificationsResponse(
            overview.Items.Select(NotificationMapper.ToResponse).ToArray(),
            overview.UnreadCount));
    }

    internal static async Task<IResult> MarkRead(Guid id, NotificationsService service, CancellationToken ct)
    {
        var notification = await service.MarkReadAsync(id, ct);
        if (notification is null)
        {
            return HttpErrors.NotFound($"Notification '{id}' was not found.");
        }

        return Results.Ok(NotificationMapper.ToResponse(notification));
    }

    internal static async Task<IResult> Reply(
        Guid id,
        ReplyNotificationRequest request,
        IValidator<ReplyNotificationRequest> validator,
        NotificationsService service,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var notification = await service.ReplyAsync(id, request.Text, ct);
        if (notification is null)
        {
            return HttpErrors.NotFound($"Notification '{id}' was not found.");
        }

        return Results.Ok(NotificationMapper.ToResponse(notification));
    }

    internal static async Task<IResult> Delete(Guid id, NotificationsService service, CancellationToken ct)
    {
        var deleted = await service.DeleteAsync(id, ct);
        if (!deleted)
        {
            return HttpErrors.NotFound($"Notification '{id}' was not found.");
        }

        return Results.NoContent();
    }

    internal static async Task<IResult> MarkAllRead(NotificationsService service, CancellationToken ct)
    {
        var marked = await service.MarkAllReadAsync(ct);

        return Results.Ok(new ReadAllNotificationsResponse(marked));
    }

    internal static async Task<IResult> ClearRead(NotificationsService service, CancellationToken ct)
    {
        var deleted = await service.ClearReadAsync(ct);

        return Results.Ok(new ClearReadNotificationsResponse(deleted));
    }

    internal static async Task<IResult> ClearConversations(NotificationsService service, CancellationToken ct)
    {
        var hidden = await service.ClearConversationsAsync(ct);

        return Results.Ok(new HideConversationsResponse(hidden));
    }
}
