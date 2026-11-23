using Microsoft.Extensions.Logging;

namespace NoMorePaper.Infrastructure.Recognition;

/// <summary>
/// Log events of the stack separation by the local LLM (Infrastructure 3990–3999, docs/kb/telemetrie.md);
/// failures of the request itself use the events of <see cref="OllamaLog"/>. Never page text, senders or answer.
/// </summary>
internal static partial class SeparationAssessorLog
{
    [LoggerMessage(EventId = 3990, Level = LogLevel.Information, Message = "Model read {Pages} pages of a stack after {ElapsedMs} ms and found {Boundaries} boundaries")]
    public static partial void StackAssessed(this ILogger logger, int pages, long elapsedMs, int boundaries);

    [LoggerMessage(EventId = 3991, Level = LogLevel.Warning, Message = "The model named {Dropped} boundaries outside the stack or twice; they were dropped")]
    public static partial void BoundariesDropped(this ILogger logger, int dropped);

    [LoggerMessage(EventId = 3992, Level = LogLevel.Information, Message = "The stack has {Pages} pages; the model reads the first {MaxPages}")]
    public static partial void StackCut(this ILogger logger, int pages, int maxPages);
}
