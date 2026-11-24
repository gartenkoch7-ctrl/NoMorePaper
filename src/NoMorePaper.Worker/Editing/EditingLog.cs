namespace NoMorePaper.Worker.Editing;

/// <summary>
/// Log events of the page backlog start (Worker 2400–2499, docs/kb/telemetrie.md); the edit stage itself
/// logs in <c>Application/Editing/EditingLog</c>. Only delays and exception types.
/// </summary>
internal static partial class EditingLog
{
    [LoggerMessage(EventId = 2400, Level = LogLevel.Warning, Message = "Could not queue the page backlog ({ErrorType}); next try in {DelaySeconds} s")]
    public static partial void PageBacklogNotQueued(this ILogger logger, string errorType, int delaySeconds);
}
