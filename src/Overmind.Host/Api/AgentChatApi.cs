using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Overmind.Core;
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
        human.MapGet("/agent/conversations/{id:guid}", GetForHuman);
        human.MapPut("/agent/conversations/{id:guid}", RenameForHuman);
        human.MapDelete("/agent/conversations/{id:guid}", DeleteForHuman);
        human.MapGet("/agent/conversations/{id:guid}/messages", HistoryForHuman);
        human.MapPost("/agent/conversations/{id:guid}/messages/stream", StreamForHuman);

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

    private static async Task<IResult> HistoryForHuman(
        Guid id,
        [FromQuery] int? limit,
        [FromQuery] DateTime? before,
        ConversationStore conversations,
        ConversationMessageStore messages,
        CancellationToken ct)
    {
        if (await conversations.GetAsync(id, ct) is null)
        {
            return NotFound(id);
        }

        var page = await messages.ListPageAsync(id, limit, before, ct);
        return Results.Ok(new ConversationHistoryResponse([.. page.Select(ToMessageResponse)]));
    }

    private static async Task<IResult> StreamForHuman(
        Guid id,
        SendConversationMessageRequest request,
        IValidator<SendConversationMessageRequest> validator,
        ConversationStore conversations,
        ConversationMessageStore messages,
        IConversationStreamSource streamSource,
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

        return Results.Stream(body => StreamReplyAsync(body, id, conversation, history, request.Text, messages, streamSource, ct), SseContentType);
    }

    private static async Task StreamReplyAsync(
        Stream body,
        Guid conversationId,
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        string userText,
        ConversationMessageStore messages,
        IConversationStreamSource streamSource,
        CancellationToken ct)
    {
        var reply = new StringBuilder();
        try
        {
            await foreach (var streamEvent in streamSource.StreamAsync(conversation, history, userText, ct))
            {
                switch (streamEvent.Kind)
                {
                    case ConversationStreamEventKind.Delta:
                        reply.Append(streamEvent.Text);
                        await WriteFrameAsync(body, "delta", new { text = streamEvent.Text }, ct);
                        break;
                    case ConversationStreamEventKind.Completed:
                        Guid? messageId = reply.Length > 0
                            ? (await messages.AppendAsync(conversationId, ConversationMessage.AssistantRole, reply.ToString(), ct)).Id
                            : null;
                        await WriteFrameAsync(body, "completed", new { messageId }, ct);
                        break;
                    case ConversationStreamEventKind.Failed:
                        await WriteFrameAsync(body, "failed", new { error = streamEvent.Text }, ct);
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            await WriteFrameAsync(body, "failed", new { error = "The chat stream failed." }, CancellationToken.None);
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
