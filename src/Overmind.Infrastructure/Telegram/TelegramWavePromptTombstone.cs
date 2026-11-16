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
            Prune(now);
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
            return _byChat.TryGetValue(chatId, out var ledger) && ledger.Contains(promptMessageId);
        }
    }

    private void Prune(DateTimeOffset now)
    {
        List<long>? dead = null;
        foreach (var (chatId, ledger) in _byChat)
        {
            if (now - ledger.LastTouchedAt > Retention)
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
        private readonly Queue<long> _order = new();

        public DateTimeOffset LastTouchedAt { get; private set; }

        public void Add(long promptMessageId, DateTimeOffset at)
        {
            LastTouchedAt = at;
            if (!_promptIds.Add(promptMessageId))
            {
                return;
            }

            _order.Enqueue(promptMessageId);
            while (_order.Count > capacity)
            {
                _promptIds.Remove(_order.Dequeue());
            }
        }

        public bool Contains(long promptMessageId) => _promptIds.Contains(promptMessageId);
    }
}
