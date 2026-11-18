using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace NoMorePaper.Application.Tests;

/// <summary>Keeps the event ids of everything logged.</summary>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    public ConcurrentQueue<int> EventIds { get; } = new();

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        EventIds.Enqueue(eventId.Id);
}
