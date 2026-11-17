namespace Overmind.Infrastructure.Telegram;

public sealed class TelegramWaveDraft
{
    public const int LifetimeMinutes = 15;

    private readonly List<string> _rooms = [];
    private long[] _promptMessageIds = [];

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
    public IReadOnlyList<long> PromptMessageIds => _promptMessageIds;

    public bool IsLive(DateTimeOffset now) => now < ExpiresAt;

    public bool AwaitsText(long promptMessageId) =>
        ProjectId is not null
        && Room is not null
        && Array.IndexOf(_promptMessageIds, promptMessageId) >= 0;

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

    public void AwaitText(string room, IReadOnlyList<long> promptMessageIds)
    {
        Room = room;
        _promptMessageIds = [.. promptMessageIds];
    }

    public void ForgetText()
    {
        Room = null;
        _promptMessageIds = [];
    }
}
