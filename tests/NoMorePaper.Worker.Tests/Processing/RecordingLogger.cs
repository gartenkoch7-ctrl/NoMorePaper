using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace NoMorePaper.Worker.Tests.Processing;

/// <summary>Keeps the event ids and rendered messages of everything logged.</summary>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    public ConcurrentQueue<(int EventId, string Message)> Entries { get; } = new();

    public IEnumerable<int> EventIds => Entries.Select(entry => entry.EventId);

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        Entries.Enqueue((eventId.Id, formatter(state, exception)));
    }
}
