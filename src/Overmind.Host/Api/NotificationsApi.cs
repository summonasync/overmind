using FluentValidation;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Mapping;
using Overmind.Infrastructure.Notifications;

namespace Overmind.Host.Api;

public static class NotificationsApi
{
    public static RouteGroupBuilder MapNotificationsApi(this IEndpointRouteBuilder routes)
    {
        var human = routes.MapGroup("/api");
        human.MapGet("/notifications", List);
        human.MapPost("/notifications/{id:guid}/read", MarkRead);
        human.MapPost("/notifications/{id:guid}/reply", Reply);
        human.MapDelete("/notifications/{id:guid}", Delete);
        human.MapPost("/notifications/read-all", MarkAllRead);
        human.MapPost("/notifications/clear-read", ClearRead);

        var agent = routes.MapGroup("/api/agent");
        agent.MapPost("/notifications", Create);
        agent.MapGet("/notifications/replies", Replies);

        return human;
    }

    internal static async Task<IResult> Create(
        CreateNotificationRequest request,
        IValidator<CreateNotificationRequest> validator,
        NotificationsService service,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var kind = Enum.Parse<NotificationKind>(request.Kind, ignoreCase: true);
        var notification = await service.CreateAsync(
            kind, request.Text, request.ProjectId, request.FeatureSlug, request.Tab, request.From, ct);

        if (notification is null)
        {
            return HttpErrors.NotFound($"Project '{request.ProjectId}' was not found.");
        }

        return Results.Created($"/api/notifications/{notification.Id}", NotificationMapper.ToResponse(notification));
    }

    internal static async Task<IResult> Replies(
        NotificationsService service,
        Guid? projectId,
        bool includeRead = true,
        CancellationToken ct = default)
    {
        var replies = await service.ListRepliesAsync(projectId, includeRead, ct);

        return Results.Ok(new RepliesResponse(replies.Select(NotificationMapper.ToResponse).ToArray()));
    }

    internal static async Task<IResult> List(NotificationsService service, CancellationToken ct)
    {
        var overview = await service.GetOverviewAsync(ct);

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
}
