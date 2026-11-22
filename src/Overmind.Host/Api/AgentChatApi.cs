using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Overmind.Core;
using Overmind.Host.AgentChat;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Validation;
using Overmind.Infrastructure.Agent;

namespace Overmind.Host.Api;

public static class AgentChatApi
{
    public const string SseContentType = "text/event-stream";

    public static RouteGroupBuilder MapAgentChatApi(this IEndpointRouteBuilder routes)
    {
        var human = routes.MapGroup("/api");
        human.MapPost("/agent/conversations", CreateForHuman);
        human.MapGet("/agent/conversations", ListForHuman);
        human.MapGet("/agent/conversations/unread", UnreadForHuman);
        human.MapGet("/agent/conversations/{id:guid}", GetForHuman);
        human.MapPut("/agent/conversations/{id:guid}", RenameForHuman);
        human.MapDelete("/agent/conversations/{id:guid}", DeleteForHuman);
        human.MapGet("/agent/conversations/{id:guid}/messages", HistoryForHuman);
        human.MapPost("/agent/conversations/{id:guid}/messages/stream", StreamForHuman);
        human.MapGet("/agent/conversations/{id:guid}/turn/stream", AttachTurnForHuman);
        human.MapPost("/agent/conversations/{id:guid}/turn/stop", StopTurnForHuman);
        human.MapPost("/agent/conversations/{id:guid}/read", MarkReadForHuman);

        return human;
    }

    private static async Task<IResult> CreateForHuman(
        CreateConversationRequest request,
        IValidator<CreateConversationRequest> validator,
        ConversationStore conversations,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        try
        {
            var conversation = await conversations.CreateAsync(request.ProjectId, request.Title, ct);
            return Results.Created($"/api/agent/conversations/{conversation.Id}", ToResponse(conversation));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    private static async Task<IResult> ListForHuman(
        [FromQuery] Guid? projectId,
        ConversationStore conversations,
        CancellationToken ct)
    {
        try
        {
            var items = await conversations.ListAsync(projectId, ct);
            return Results.Ok(new ConversationsResponse([.. items.Select(ToResponse)]));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    private static async Task<IResult> GetForHuman(Guid id, ConversationStore conversations, CancellationToken ct)
    {
        var conversation = await conversations.GetAsync(id, ct);
        return conversation is null
            ? NotFound(id)
            : Results.Ok(ToResponse(conversation));
    }

    private static async Task<IResult> RenameForHuman(
        Guid id,
        RenameConversationRequest request,
        IValidator<RenameConversationRequest> validator,
        ConversationStore conversations,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var conversation = await conversations.RenameAsync(id, request.Title, ct);
        return conversation is null
            ? NotFound(id)
            : Results.Ok(ToResponse(conversation));
    }

    private static async Task<IResult> DeleteForHuman(Guid id, ConversationStore conversations, CancellationToken ct)
    {
        var deleted = await conversations.DeleteAsync(id, ct);
        return deleted ? Results.NoContent() : NotFound(id);
    }

    private static async Task<IResult> MarkReadForHuman(
        Guid id,
        MarkConversationReadRequest request,
        IValidator<MarkConversationReadRequest> validator,
        ConversationReadStore reads,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        try
        {
            var conversation = await reads.MarkReadAsync(id, request.LastReadMessageId, ct);
            return Results.Ok(new ConversationReadStateResponse(conversation.Id, conversation.LastReadMessageId));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    private static async Task<IResult> UnreadForHuman(ConversationReadStore reads, CancellationToken ct) =>
        Results.Ok(new UnreadCountResponse(await reads.CountUnreadAsync(ct)));

    private static async Task<IResult> HistoryForHuman(
        Guid id,
        [FromQuery] int? limit,
        [FromQuery] DateTime? before,
        ConversationStore conversations,
        ConversationMessageStore messages,
        ConversationTurnRegistry turns,
        CancellationToken ct)
    {
        if (await conversations.GetAsync(id, ct) is null)
        {
            return NotFound(id);
        }

        var page = await messages.ListPageAsync(id, limit, before, ct);
        return Results.Ok(new ConversationHistoryResponse(
            [.. page.Select(ToMessageResponse)],
            TurnActive: turns.Find(id) is not null));
    }

    private static async Task<IResult> StreamForHuman(
        Guid id,
        SendConversationMessageRequest request,
        IValidator<SendConversationMessageRequest> validator,
        ConversationStore conversations,
        ConversationMessageStore messages,
        ConversationTurnRegistry turns,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var conversation = await conversations.GetAsync(id, ct);
        if (conversation is null)
        {
            return NotFound(id);
        }

        var history = await messages.ListPageAsync(id, ct: ct);
        await messages.AppendAsync(id, ConversationMessage.UserRole, request.Text, ct);

        var turn = turns.TryCreate(conversation);
        if (turn is null)
        {
            return HttpErrors.Conflict(
                "A reply is already being generated for this conversation. Stop it or wait for it to finish.",
                "Turn Already Running");
        }

        var events = turn.Subscribe(ct);
        turns.Start(turn, conversation, history, request.Text);
        return Results.Stream(body => StreamTurnAsync(body, events), SseContentType);
    }

    private static async Task<IResult> AttachTurnForHuman(
        Guid id,
        ConversationStore conversations,
        ConversationTurnRegistry turns,
        CancellationToken ct)
    {
        var conversation = await conversations.GetAsync(id, ct);
        if (conversation is null)
        {
            return NotFound(id);
        }

        var turn = turns.Find(id);
        if (turn is null)
        {
            return Results.NoContent();
        }

        var events = turn.Subscribe(ct);
        return Results.Stream(body => StreamTurnAsync(body, events), SseContentType);
    }

    private static async Task<IResult> StopTurnForHuman(
        Guid id,
        ConversationStore conversations,
        ConversationTurnRegistry turns,
        CancellationToken ct)
    {
        var conversation = await conversations.GetAsync(id, ct);
        return conversation is null
            ? NotFound(id)
            : Results.Ok(new ConversationTurnStopResponse(turns.TryStop(id)));
    }

    private static async Task StreamTurnAsync(
        Stream body,
        IAsyncEnumerable<ConversationStreamEvent> events)
    {
        await foreach (var streamEvent in events)
        {
            switch (streamEvent.Kind)
            {
                case ConversationStreamEventKind.Delta:
                    await WriteFrameAsync(body, "delta", new { text = streamEvent.Text }, CancellationToken.None);
                    break;
                case ConversationStreamEventKind.ToolCall:
                    await WriteFrameAsync(body, "tool", new { tool = streamEvent.ToolName }, CancellationToken.None);
                    break;
                case ConversationStreamEventKind.Routed:
                    await WriteFrameAsync(
                        body,
                        "route",
                        new { target = streamEvent.RouteTarget, source = streamEvent.RouteSource?.ToString().ToLowerInvariant() },
                        CancellationToken.None);
                    break;
                case ConversationStreamEventKind.Bounced:
                    break;
                case ConversationStreamEventKind.Completed:
                    await WriteFrameAsync(
                        body, "completed", new { messageId = streamEvent.MessageId, tokensUsed = streamEvent.TokensUsed }, CancellationToken.None);
                    break;
                case ConversationStreamEventKind.Failed:
                    await WriteFrameAsync(body, "failed", new { error = streamEvent.Text, code = streamEvent.Code }, CancellationToken.None);
                    break;
                case ConversationStreamEventKind.Aborted:
                    await WriteFrameAsync(body, "aborted", new { }, CancellationToken.None);
                    break;
                default:
                    await WriteFrameAsync(
                        body, "failed", new { error = "The chat stream sent an unknown event.", code = "unknownStreamEvent" }, CancellationToken.None);
                    break;
            }
        }
    }

    private static async Task WriteFrameAsync(Stream body, string eventName, object payload, CancellationToken ct)
    {
        await body.WriteAsync(Encoding.UTF8.GetBytes($"event: {eventName}\n"), ct);
        await body.WriteAsync(Encoding.UTF8.GetBytes($"data: {JsonSerializer.Serialize(payload)}\n\n"), ct);
        await body.FlushAsync(ct);
    }

    private static ConversationResponse ToResponse(Conversation conversation) =>
        new(conversation.Id, conversation.ProjectId, conversation.Title, conversation.CreatedAt, conversation.UpdatedAt);

    private static ConversationMessageResponse ToMessageResponse(ConversationMessage message) =>
        new(message.Id, message.Role, message.Content, message.CreatedAt);

    private static IResult NotFound(Guid id) =>
        HttpErrors.NotFound($"Conversation '{id}' was not found.");
}
