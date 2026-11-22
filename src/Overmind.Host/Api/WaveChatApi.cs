using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.WaveChat;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Wave;

namespace Overmind.Host.Api;

public static class WaveChatApi
{
    private const string SseContentType = "text/event-stream";

    private static readonly JsonSerializerOptions FrameJson = new(JsonSerializerDefaults.Web);
    private static readonly byte[] HeartbeatFrame = ": ping\n\n"u8.ToArray();

    public static RouteGroupBuilder MapWaveChatApi(this IEndpointRouteBuilder routes)
    {
        var human = routes.MapGroup("/api");
        human.MapPost("/projects/{projectId:guid}/wave/rooms/{room}/messages", PostForHuman);
        human.MapGet("/projects/{projectId:guid}/wave/rooms/{room}/unread", UnreadForHuman);
        human.MapGet("/projects/{projectId:guid}/wave/rooms/{room}/messages", HistoryForHuman);
        human.MapGet("/projects/{projectId:guid}/wave/rooms", RoomsForHuman);
        human.MapGet("/projects/{projectId:guid}/wave/stream", StreamForHuman);

        var agent = routes.MapAgentGroup();
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

        return await UnreadAsync(projectId, alias, room, since: null, limit: null, replayMentions: false, chat, ct);
    }

    private static async Task<IResult> UnreadForAgent(
        Guid projectId,
        string room,
        [FromQuery] DateTime? since,
        [FromQuery] int? limit,
        WaveChatService chat,
        OvermindDbContext db,
        HttpRequest http,
        [FromQuery] bool replayMentions = false,
        CancellationToken ct = default)
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

        return await UnreadAsync(projectId, session.Alias, room, since, limit, replayMentions, chat, ct);
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

    private static async Task<IResult> StreamForHuman(
        Guid projectId,
        [FromQuery] DateTime? since,
        [FromQuery] Guid? sinceId,
        WaveChatService chat,
        WaveSseHub hub,
        WaveStreamOptions options,
        OvermindDbContext db,
        CancellationToken ct)
    {
        if (await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct) is null)
        {
            return PlanningErrors.Map(PlanningException.MissingProject(projectId));
        }

        return Results.Stream(
            body => StreamProjectAsync(body, hub, chat, projectId, since, sinceId, options, ct), SseContentType);
    }

    private static async Task StreamProjectAsync(
        Stream body,
        WaveSseHub hub,
        WaveChatService chat,
        Guid projectId,
        DateTime? since,
        Guid? sinceId,
        WaveStreamOptions options,
        CancellationToken ct)
    {
        await using var subscription = hub.Subscribe(projectId);
        await body.FlushAsync(ct);

        HashSet<Guid>? seen = null;
        if (since is { } anchor)
        {
            var backfill = await chat.ListSinceAsync(projectId, anchor, sinceId, ct: ct);
            seen = [.. backfill.Select(m => m.Id)];
            foreach (var message in backfill)
            {
                await WriteFrameAsync(body, message, ct);
            }
        }

        var dedupOpen = seen is not null;
        using var heartbeat = new PeriodicTimer(options.HeartbeatInterval);
        CancellationTokenSource tickGate = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task<bool> pending = subscription.Messages.WaitToReadAsync(ct).AsTask();

        try
        {
            while (true)
            {
                Task<bool> ticked = heartbeat.WaitForNextTickAsync(tickGate.Token).AsTask();

                if (await Task.WhenAny(pending, ticked) != ticked)
                {
                    tickGate.Cancel();
                    try
                    {
                        await ticked;
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    tickGate.Dispose();
                    tickGate = CancellationTokenSource.CreateLinkedTokenSource(ct);

                    if (!await pending)
                    {
                        break;
                    }

                    while (subscription.Messages.TryRead(out var message))
                    {
                        if (seen is { } window && !(dedupOpen ? window.Add(message.Id) : window.Contains(message.Id)))
                        {
                            continue;
                        }

                        await WriteFrameAsync(body, message, ct);
                    }

                    dedupOpen = false;
                    pending = subscription.Messages.WaitToReadAsync(ct).AsTask();
                    continue;
                }

                if (!await ticked)
                {
                    break;
                }

                await body.WriteAsync(HeartbeatFrame, ct);
                await body.FlushAsync(ct);
            }
        }
        finally
        {
            tickGate.Dispose();
        }
    }

    private static async Task WriteFrameAsync(Stream body, WaveMessage message, CancellationToken ct)
    {
        await body.WriteAsync("event: message\n"u8.ToArray(), ct);
        await body.WriteAsync(
            Encoding.UTF8.GetBytes(
                $"data: {JsonSerializer.Serialize(new WaveStreamFrame(message.Room, ToResponse(message)), FrameJson)}\n\n"),
            ct);
        await body.FlushAsync(ct);
    }

    private static async Task<IResult> PostAsync(
        Guid projectId, string room, string authorAlias, string authorRole, string text, WaveChatService chat, CancellationToken ct)
    {
        var warnings = new List<object>();
        try
        {
            // Soft cap 266: over-cap signal text is stored whole inside the grace band and
            // truncated to the keep length beyond it; both cases answer with warnings[].
            var capped = SoftCapSoften.Cap(text, WaveMessage.MaxTextLength, warnings);
            var message = await chat.PostAsync(projectId, RouteNameDecoder.Decode(room), authorAlias, authorRole, capped!, ct);
            return Results.Ok(ToResponse(message) with { Warnings = SoftCapSoften.Merge(warnings) });
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
        Guid projectId,
        string alias,
        string room,
        DateTime? since,
        int? limit,
        bool replayMentions,
        WaveChatService chat,
        CancellationToken ct)
    {
        try
        {
            var page = await chat.ListUnreadAsync(projectId, alias, RouteNameDecoder.Decode(room), since, limit, replayMentions, ct);
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
            var peek = await chat.PeekAsync(projectId, alias, RouteNameDecoder.Decode(room), ct);
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
            var messages = await chat.ListHistoryAsync(projectId, RouteNameDecoder.Decode(room), limit, before, ct);
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
