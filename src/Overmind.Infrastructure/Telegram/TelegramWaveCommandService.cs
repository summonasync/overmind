using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Wave;
using Telegram.Bot.Types.ReplyMarkups;

namespace Overmind.Infrastructure.Telegram;

public sealed class TelegramWaveCommandService(
    TelegramReplySender sender,
    TelegramWaveDraftStore drafts,
    TelegramWavePromptTombstone prompts,
    WaveChatService chat,
    OvermindDbContext db,
    ITelegramWaveSnapshotService snapshot,
    ILogger<TelegramWaveCommandService> logger) : ITelegramWaveCommandService
{
    public const int MaxProjectButtons = 40;

    public const string ProjectPickerText = "Which project owns this signal?";
    public const string PickerTruncationFormat =
        "Which project owns this signal? (the first {0} by name, {1} more not shown)";
    public const string RoomPickerTextFormat = "Which room in {0}?";
    public const string PromptTextFormat =
        "Reply to this message with the signal for {0}. It is posted verbatim.";
    public const string PostedTextFormat = "Posted to {0}.";
    public const string TooLongTextFormat =
        "That is {0} characters; a wave signal is capped at {1}. Shorten it and reply again.";
    public const string EmptyTextReply = "The reply carried no text, so nothing was posted.";
    public const string PostRefusedReplyFormat =
        "The {0} room refused the post. Run /send_to_wave to start over.";
    public const string NoProjectsReply = "No projects are registered, so there is nothing to post to.";
    public const string DraftClosedReply =
        "That wave draft is closed (expired, cancelled, or already posted). Run /send_to_wave again to send a new signal.";
    public const string RoomPickAnswer = "Pick the room.";
    public const string PromptAnswer = "Reply with the signal.";
    public const string CancelAnswer = "Cancelled.";

    // The role is stamped "owner" on purpose: WaveMessage collapses any role outside
    // owner/marshal/embedded to "worker", so "telegram" would be stored as a worker post.
    public const string AuthorRole = WaveMessage.OwnerRole;
    public const string AnonymousAuthor = "telegram";

    public async Task HandleAsync(TelegramWaveCommand command, TelegramIncomingMessage message, CancellationToken ct)
    {
        logger.LogInformation(
            "Telegram wave command {Command} invoked in chat {ChatId} by {Sender} over message {MessageId} of update {UpdateId}",
            command, message.ChatId, message.From ?? AnonymousAuthor, message.MessageId, message.UpdateId);

        if (command == TelegramWaveCommand.WaveSnapshot)
        {
            await snapshot.HandleCommandAsync(message.ChatId, ct);
            return;
        }

        var (choices, hidden) = await ListProjectsAsync(ct);
        if (choices.Count == 0)
        {
            await SendQuietlyAsync(message.ChatId, NoProjectsReply, ct: ct);
            return;
        }

        drafts.Open(message.ChatId);
        logger.LogInformation(
            "Telegram wave draft opened in chat {ChatId} over {ProjectCount} projects",
            message.ChatId, choices.Count);

        await SendQuietlyAsync(
            message.ChatId,
            hidden > 0 ? string.Format(PickerTruncationFormat, choices.Count, hidden) : ProjectPickerText,
            ct,
            TelegramWaveKeyboardMarkup.Projects(choices));
    }

    public async Task<bool> TryHandleDraftReplyAsync(TelegramIncomingMessage message, CancellationToken ct)
    {
        if (message.RepliedToMessageId is not { } promptMessageId)
        {
            return false;
        }

        var draft = drafts.AwaitingText(message.ChatId, promptMessageId);
        if (draft is null || draft.ProjectId is not { } projectId || draft.Room is not { } room)
        {
            if (!prompts.IsClosed(message.ChatId, promptMessageId))
            {
                return false;
            }

            logger.LogInformation(
                "Telegram wave draft in chat {ChatId} was closed; reply message {MessageId} of update {UpdateId} to prompt {PromptMessageId} got the restart notice instead of an agent turn",
                message.ChatId, message.MessageId, message.UpdateId, promptMessageId);
            await SendQuietlyAsync(message.ChatId, DraftClosedReply, ct: ct);
            return true;
        }

        var text = message.Text.Trim();
        if (text.Length == 0)
        {
            await SendQuietlyAsync(message.ChatId, EmptyTextReply, ct: ct);
            return true;
        }

        if (text.Length > WaveMessage.MaxTextLength)
        {
            await SendQuietlyAsync(
                message.ChatId,
                string.Format(TooLongTextFormat, text.Length, WaveMessage.MaxTextLength),
                ct: ct);
            return true;
        }

        try
        {
            var posted = await chat.PostAsync(projectId, room, AuthorAlias(message.From), AuthorRole, text, ct);
            draft.ForgetText();
            logger.LogInformation(
                "Telegram chat {ChatId} posted {TextLength} characters into room {Room} of project {ProjectId} as {AuthorAlias} with role {AuthorRole}",
                message.ChatId, posted.Text.Length, posted.Room, posted.ProjectId, posted.AuthorAlias,
                posted.AuthorRole);
            await SendQuietlyAsync(message.ChatId, string.Format(PostedTextFormat, posted.Room), ct: ct);
        }
        catch (PlanningException ex)
        {
            drafts.Cancel(message.ChatId);
            logger.LogWarning(
                ex,
                "Telegram wave post from chat {ChatId} into room {Room} of project {ProjectId} was refused; the draft was dropped",
                message.ChatId, room, projectId);
            await SendQuietlyAsync(message.ChatId, string.Format(PostRefusedReplyFormat, room), ct: ct);
        }

        return true;
    }

    public async Task<string> PickProjectAsync(long chatId, Guid projectId, CancellationToken ct)
    {
        if (drafts.Current(chatId) is not { } draft)
        {
            return TelegramNotificationCallbackHandler.InvalidButtonAnswer;
        }

        var project = await db.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new { p.Id, p.Name })
            .FirstOrDefaultAsync(ct);
        if (project is null)
        {
            logger.LogWarning(
                "Telegram wave project pick {ProjectId} in chat {ChatId} no longer resolves",
                projectId, chatId);
            return TelegramNotificationCallbackHandler.FailureAnswer;
        }

        var rooms = await chat.ListRoomsAsync(projectId, ct);
        draft.MarkProject(project.Id, project.Name, [.. rooms.Select(room => room.Room)]);

        await sender.SendAsync(
            chatId,
            TelegramReplySplitter.Split(string.Format(RoomPickerTextFormat, project.Name)),
            TelegramWaveKeyboardMarkup.Rooms(draft.Token, draft.Rooms),
            ct: ct);
        return RoomPickAnswer;
    }

    public async Task<string> PickRoomAsync(long chatId, string draftToken, int roomIndex, CancellationToken ct)
    {
        if (drafts.Current(chatId, draftToken) is not { } draft
            || !draft.TryResolveRoom(roomIndex, out var room))
        {
            return TelegramNotificationCallbackHandler.InvalidButtonAnswer;
        }

        var target = draft.ProjectName is { } name ? $"{name} · {room}" : room;
        var delivered = await sender.SendAsync(
            chatId,
            TelegramReplySplitter.Split(string.Format(PromptTextFormat, target)),
            TelegramWaveKeyboardMarkup.Prompt(),
            ct: ct);
        if (delivered.Count == 0)
        {
            return TelegramNotificationCallbackHandler.FailureAnswer;
        }

        draft.AwaitText(room, delivered);
        foreach (var promptMessageId in delivered)
        {
            prompts.Announce(chatId, promptMessageId);
        }

        return PromptAnswer;
    }

    public string Cancel(long chatId)
    {
        if (drafts.Cancel(chatId))
        {
            logger.LogInformation("Telegram wave draft in chat {ChatId} was cancelled", chatId);
        }

        return CancelAnswer;
    }

    private async Task<(IReadOnlyList<TelegramWaveProjectChoice> Choices, int Hidden)> ListProjectsAsync(
        CancellationToken ct)
    {
        var rows = await db.Projects
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name })
            .Take(MaxProjectButtons + 1)
            .ToListAsync(ct);

        var hidden = 0;
        if (rows.Count > MaxProjectButtons)
        {
            hidden = await db.Projects.CountAsync(ct) - MaxProjectButtons;
            rows = [.. rows.Take(MaxProjectButtons)];
        }

        return ([.. rows.Select(p => new TelegramWaveProjectChoice(p.Id, p.Name))], hidden);
    }

    private static string AuthorAlias(string? from)
    {
        var name = (from ?? string.Empty).TrimStart('@').Trim();
        if (name.Length == 0)
        {
            return AnonymousAuthor;
        }

        return name.Length <= WaveMessage.MaxIdentityLength
            ? name
            : name[..WaveMessage.MaxIdentityLength];
    }

    private async Task SendQuietlyAsync(
        long chatId,
        string text,
        CancellationToken ct,
        ReplyMarkup? markup = null)
    {
        try
        {
            await sender.SendAsync(chatId, TelegramReplySplitter.Split(text), markup, ct: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Telegram wave notice to chat {ChatId} could not be delivered", chatId);
        }
    }
}
