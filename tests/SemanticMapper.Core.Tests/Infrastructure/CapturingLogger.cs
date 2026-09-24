using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SemanticMapper.Core.Tests.Infrastructure;

public sealed record LogEntry(LogLevel Level, EventId EventId, string Message, IReadOnlyList<KeyValuePair<string, object?>> State);

public sealed class CapturingLogger<T> : ILogger<T>
{
    public ConcurrentQueue<LogEntry> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var properties = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];
        Entries.Enqueue(new LogEntry(logLevel, eventId, formatter(state, exception), [.. properties]));
    }

    public bool Contains(string eventName) => Entries.Any(e => e.EventId.Name == eventName);

    /// <summary>All rendered messages plus every structured property value, as text.</summary>
    public string AllText() => string.Join(
        Environment.NewLine,
        Entries.SelectMany(e => e.State.Select(p => $"{p.Key}={p.Value}").Prepend(e.Message)));
}
