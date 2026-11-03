using Microsoft.Extensions.Logging;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class CapturingLogger<T>() : ILogger<T>
{
    public sealed record Entry(LogLevel Level, string Message, IReadOnlyList<KeyValuePair<string, object?>> Values);

    public List<Entry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var values = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];
        Entries.Add(new Entry(logLevel, formatter(state, exception), [.. values]));
    }
}
