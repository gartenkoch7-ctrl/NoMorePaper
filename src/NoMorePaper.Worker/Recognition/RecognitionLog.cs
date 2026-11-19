namespace NoMorePaper.Worker.Recognition;

/// <summary>
/// Log events of the recognition start (Worker 2200–2299, docs/kb/telemetrie.md); recognition itself
/// logs in <c>Application/Recognition/RecognitionLog</c>, the LLM client in <c>Infrastructure/Recognition/OllamaLog</c>.
/// Only durations, delays and exception types.
/// </summary>
internal static partial class RecognitionLog
{
    [LoggerMessage(EventId = 2200, Level = LogLevel.Information, Message = "Preparing the recognition model")]
    public static partial void PreparingModel(this ILogger logger);

    [LoggerMessage(EventId = 2201, Level = LogLevel.Warning, Message = "Recognition model not ready ({ErrorType}); next try in {DelaySeconds} s")]
    public static partial void ModelNotReady(this ILogger logger, string errorType, int delaySeconds);

    [LoggerMessage(EventId = 2202, Level = LogLevel.Information, Message = "Recognition model ready after {ElapsedMs} ms")]
    public static partial void ModelReady(this ILogger logger, long elapsedMs);

    [LoggerMessage(EventId = 2203, Level = LogLevel.Warning, Message = "Could not queue the recognition backlog ({ErrorType}); next try in {DelaySeconds} s")]
    public static partial void BacklogNotQueued(this ILogger logger, string errorType, int delaySeconds);
}
