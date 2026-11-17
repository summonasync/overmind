namespace Overmind.Infrastructure.Telegram;

public sealed class TelegramWavePromptTombstone(TimeProvider clock)
{
    public const int MaxPromptIdsPerChat = 32;
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    private readonly object _gate = new();
    private readonly Dictionary<long, Ledger> _byChat = [];

    public void Announce(long chatId, long promptMessageId)
    {
        lock (_gate)
        {
            var now = clock.GetUtcNow();
            DropExpiredPromptIds(now);
            if (!_byChat.TryGetValue(chatId, out var ledger))
            {
                ledger = new Ledger(MaxPromptIdsPerChat);
                _byChat[chatId] = ledger;
            }

            ledger.Add(promptMessageId, now);
        }
    }

    public bool IsClosed(long chatId, long promptMessageId)
    {
        lock (_gate)
        {
            if (!_byChat.TryGetValue(chatId, out var ledger))
            {
                return false;
            }

            var closed = ledger.Contains(promptMessageId, clock.GetUtcNow());
            if (ledger.IsEmpty)
            {
                _byChat.Remove(chatId);
            }

            return closed;
        }
    }

    private void DropExpiredPromptIds(DateTimeOffset now)
    {
        List<long>? dead = null;
        foreach (var (chatId, ledger) in _byChat)
        {
            ledger.Expire(now);
            if (ledger.IsEmpty)
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

    private sealed class Ledger(int capacity)
    {
        private readonly HashSet<long> _promptIds = [];
        private readonly Queue<(long Id, DateTimeOffset At)> _order = new();

        public bool IsEmpty => _order.Count == 0;

        public void Add(long promptMessageId, DateTimeOffset at)
        {
            if (!_promptIds.Add(promptMessageId))
            {
                return;
            }

            _order.Enqueue((promptMessageId, at));
            while (_order.Count > capacity)
            {
                _promptIds.Remove(_order.Dequeue().Id);
            }
        }

        public bool Contains(long promptMessageId, DateTimeOffset now)
        {
            Expire(now);
            return _promptIds.Contains(promptMessageId);
        }

        public void Expire(DateTimeOffset now)
        {
            while (_order.Count > 0 && now - _order.Peek().At > Retention)
            {
                _promptIds.Remove(_order.Dequeue().Id);
            }
        }
    }
}
