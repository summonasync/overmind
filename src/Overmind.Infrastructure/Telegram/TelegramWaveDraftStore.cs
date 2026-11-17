using System.Text;

namespace Overmind.Infrastructure.Telegram;

public sealed class TelegramWaveDraftStore(TimeProvider clock)
{
    private readonly object _gate = new();
    private readonly Dictionary<long, TelegramWaveDraft> _byChat = [];

    public TelegramWaveDraft Open(long chatId)
    {
        var draft = new TelegramWaveDraft(chatId, NewToken(), clock.GetUtcNow());
        lock (_gate)
        {
            PruneExpired();
            _byChat[draft.ChatId] = draft;
        }

        return draft;
    }

    public TelegramWaveDraft? Current(long chatId)
    {
        lock (_gate)
        {
            if (!_byChat.TryGetValue(chatId, out var draft))
            {
                return null;
            }

            if (!draft.IsLive(clock.GetUtcNow()))
            {
                _byChat.Remove(chatId);
                return null;
            }

            return draft;
        }
    }

    public TelegramWaveDraft? Current(long chatId, string draftToken) =>
        Current(chatId) is { } draft && draft.Token == draftToken ? draft : null;

    public TelegramWaveDraft? AwaitingText(long chatId, long promptMessageId) =>
        Current(chatId) is { } draft && draft.AwaitsText(promptMessageId) ? draft : null;

    public bool Cancel(long chatId)
    {
        lock (_gate)
        {
            return _byChat.Remove(chatId);
        }
    }

    private void PruneExpired()
    {
        var now = clock.GetUtcNow();
        List<long>? dead = null;
        foreach (var (chatId, draft) in _byChat)
        {
            if (!draft.IsLive(now))
            {
                (dead ??= []).Add(chatId);
            }
        }

        if (dead is null)
        {
            return;
        }

        foreach (var chatId in dead)
        {
            _byChat.Remove(chatId);
        }
    }

    private static string NewToken()
    {
        var guid = Guid.NewGuid().ToString("N");
        var builder = new StringBuilder(TelegramWaveCallbackData.DraftTokenLength);
        foreach (var character in guid)
        {
            if (builder.Length == TelegramWaveCallbackData.DraftTokenLength)
            {
                break;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
