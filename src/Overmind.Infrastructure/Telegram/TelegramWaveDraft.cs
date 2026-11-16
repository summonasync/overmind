namespace Overmind.Infrastructure.Telegram;

public sealed class TelegramWaveDraft
{
    public const int LifetimeMinutes = 15;

    private readonly List<string> _rooms = [];

    public TelegramWaveDraft(long chatId, string token, DateTimeOffset openedAt)
    {
        ChatId = chatId;
        Token = token;
        ExpiresAt = openedAt.AddMinutes(LifetimeMinutes);
    }

    public string Token { get; }
    public long ChatId { get; }
    public DateTimeOffset ExpiresAt { get; }
    public Guid? ProjectId { get; private set; }
    public string? ProjectName { get; private set; }
    public IReadOnlyList<string> Rooms => _rooms;
    public string? Room { get; private set; }
    public long? PromptMessageId { get; private set; }

    public bool IsLive(DateTimeOffset now) => now < ExpiresAt;

    public bool AwaitsText(long promptMessageId) =>
        PromptMessageId == promptMessageId && ProjectId is not null && Room is not null;

    public void MarkProject(Guid projectId, string projectName, IReadOnlyList<string> rooms)
    {
        ProjectId = projectId;
        ProjectName = projectName;
        _rooms.Clear();
        _rooms.AddRange(rooms);
        ForgetText();
    }

    public bool TryResolveRoom(int roomIndex, out string room)
    {
        if (roomIndex < 0 || roomIndex >= _rooms.Count)
        {
            room = string.Empty;
            return false;
        }

        room = _rooms[roomIndex];
        return true;
    }

    public void AwaitText(string room, long promptMessageId)
    {
        Room = room;
        PromptMessageId = promptMessageId;
    }

    public void ForgetText()
    {
        Room = null;
        PromptMessageId = null;
    }
}
