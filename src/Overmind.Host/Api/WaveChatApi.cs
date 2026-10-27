using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Wave;

namespace Overmind.Host.Api;

public static class WaveChatApi
{
    public static RouteGroupBuilder MapWaveChatApi(this IEndpointRouteBuilder routes)
    {
        var human = routes.MapGroup("/api");
        human.MapPost("/projects/{projectId:guid}/wave/rooms/{room}/messages", PostForHuman);
        human.MapGet("/projects/{projectId:guid}/wave/rooms/{room}/unread", UnreadForHuman);
        human.MapGet("/projects/{projectId:guid}/wave/rooms/{room}/messages", HistoryForHuman);
        human.MapGet("/projects/{projectId:guid}/wave/rooms", RoomsForHuman);

        var agent = routes.MapGroup("/api/agent");
        agent.MapPost("/projects/{projectId:guid}/wave/rooms/{room}/messages", PostForAgent);
        agent.MapGet("/projects/{projectId:guid}/wave/rooms/{room}/unread", UnreadForAgent);
        agent.MapGet("/projects/{projectId:guid}/wave/rooms/{room}/peek", PeekForAgent);
        agent.MapGet("/projects/{projectId:guid}/wave/rooms/{room}/messages", HistoryForAgent);
        agent.MapGet("/projects/{projectId:guid}/wave/rooms", RoomsForHuman);

        return human;
    }

    private static async Task<IResult> PostForHuman(
        Guid projectId,
        string room,
        PostWaveMessageHumanRequest request,
        IValidator<PostWaveMessageHumanRequest> validator,
        WaveChatService chat,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        return await PostAsync(projectId, room, request.AuthorAlias, WaveParticipantResponse.OwnerRole, request.Text, chat, ct);
    }

    private static async Task<IResult> PostForAgent(
        Guid projectId,
        string room,
        PostWaveMessageRequest request,
        IValidator<PostWaveMessageRequest> validator,
        WaveChatService chat,
        OvermindDbContext db,
        HttpRequest http,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        if (!SessionHeader.TryRead(http, out var sessionId))
        {
            return AgentIdentityRequired();
        }

        var session = await db.AgentSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null)
        {
            return AgentIdentityRequired();
        }

        return await PostAsync(projectId, room, session.Alias, session.Role, request.Text, chat, ct);
    }

    private static async Task<IResult> UnreadForHuman(
        Guid projectId,
        string room,
        [FromQuery] string? alias,
        WaveChatService chat,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["alias"] = ["The alias query parameter is required."]
            });
        }

        return await UnreadAsync(projectId, alias, room, chat, ct);
    }

    private static async Task<IResult> UnreadForAgent(
        Guid projectId,
        string room,
        WaveChatService chat,
        OvermindDbContext db,
        HttpRequest http,
        CancellationToken ct)
    {
        if (!SessionHeader.TryRead(http, out var sessionId))
        {
            return AgentIdentityRequired();
        }

        var session = await db.AgentSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null)
        {
            return AgentIdentityRequired();
        }

        return await UnreadAsync(projectId, session.Alias, room, chat, ct);
    }

    private static async Task<IResult> PeekForAgent(
        Guid projectId,
        string room,
        WaveChatService chat,
        OvermindDbContext db,
        HttpRequest http,
        CancellationToken ct)
    {
        if (!SessionHeader.TryRead(http, out var sessionId))
        {
            return AgentIdentityRequired();
        }

        var session = await db.AgentSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null)
        {
            return AgentIdentityRequired();
        }

        return await PeekAsync(projectId, session.Alias, room, chat, ct);
    }

    private static async Task<IResult> HistoryForHuman(
        Guid projectId,
        string room,
        [FromQuery] int? limit,
        [FromQuery] DateTime? before,
        WaveChatService chat,
        CancellationToken ct)
    {
        return await HistoryAsync(projectId, room, limit, before, chat, ct);
    }

    private static async Task<IResult> HistoryForAgent(
        Guid projectId,
        string room,
        [FromQuery] int? limit,
        [FromQuery] DateTime? before,
        WaveChatService chat,
        OvermindDbContext db,
        HttpRequest http,
        CancellationToken ct)
    {
        if (!SessionHeader.TryRead(http, out var sessionId))
        {
            return AgentIdentityRequired();
        }

        var session = await db.AgentSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null)
        {
            return AgentIdentityRequired();
        }

        return await HistoryAsync(projectId, room, limit, before, chat, ct);
    }

    private static async Task<IResult> RoomsForHuman(
        Guid projectId, WaveChatService chat, CancellationToken ct)
    {
        try
        {
            var rooms = await chat.ListRoomsAsync(projectId, ct);
            return Results.Ok(new WaveRoomsResponse(
            [
                .. rooms.Select(r => new WaveRoomResponse(r.Room, r.LastActivityAt))
            ]));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    private static async Task<IResult> PostAsync(
        Guid projectId, string room, string authorAlias, string authorRole, string text, WaveChatService chat, CancellationToken ct)
    {
        try
        {
            var message = await chat.PostAsync(projectId, room, authorAlias, authorRole, text, ct);
            return Results.Ok(ToResponse(message));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
        catch (ArgumentException ex)
        {
            return HttpErrors.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> UnreadAsync(
        Guid projectId, string alias, string room, WaveChatService chat, CancellationToken ct)
    {
        try
        {
            var page = await chat.ListUnreadAsync(projectId, alias, room, ct: ct);
            return Results.Ok(new WaveUnreadResponse(
            [
                .. page.Messages.Select(ToResponse)
            ], page.WatermarkAt));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
        catch (ArgumentException ex)
        {
            return HttpErrors.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> PeekAsync(
        Guid projectId, string alias, string room, WaveChatService chat, CancellationToken ct)
    {
        try
        {
            var peek = await chat.PeekAsync(projectId, alias, room, ct);
            return Results.Ok(new WavePeekResponse(peek.Room, peek.Count, [.. peek.Ids]));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
        catch (ArgumentException ex)
        {
            return HttpErrors.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> HistoryAsync(
        Guid projectId,
        string room,
        int? limit,
        DateTime? before,
        WaveChatService chat,
        CancellationToken ct)
    {
        try
        {
            var messages = await chat.ListHistoryAsync(projectId, room, limit, before, ct);
            return Results.Ok(new WaveHistoryResponse(
            [
                .. messages.Select(ToResponse)
            ]));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    private static WaveMessageResponse ToResponse(WaveMessage message) =>
        new(message.Id,
            message.Room,
            message.AuthorAlias,
            message.Text,
            message.MentionList,
            message.CreatedAt,
            message.AuthorRole);

    private static IResult AgentIdentityRequired() =>
        HttpErrors.BadRequest(
            $"Wave chat authorship requires the {SessionHeader.Name} header and a registered agent session — " +
            "register via POST /api/agent/sessions.");
}
