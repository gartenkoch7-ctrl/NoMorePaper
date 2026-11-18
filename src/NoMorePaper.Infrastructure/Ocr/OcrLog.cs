using Microsoft.Extensions.Logging;

namespace NoMorePaper.Infrastructure.Ocr;

/// <summary>
/// Log events of OCR and its external tools (Infrastructure 3300–3399, docs/kb/telemetrie.md).
/// Only tool names, exit codes, durations and counts: tool output and paths may quote documents.
/// </summary>
internal static partial class OcrLog
{
    [LoggerMessage(EventId = 3300, Level = LogLevel.Debug, Message = "{Tool} exited with code {ExitCode} after {ElapsedMs} ms")]
    public static partial void ToolExited(this ILogger logger, string tool, int exitCode, long elapsedMs);

    [LoggerMessage(EventId = 3301, Level = LogLevel.Warning, Message = "{Tool} exceeded its time limit of {TimeoutSeconds} s and was stopped")]
    public static partial void ToolTimedOut(this ILogger logger, string tool, double timeoutSeconds);

    [LoggerMessage(EventId = 3302, Level = LogLevel.Information, Message = "{Tool} was stopped because processing was cancelled")]
    public static partial void ToolCancelled(this ILogger logger, string tool);

    [LoggerMessage(EventId = 3303, Level = LogLevel.Information, Message = "Recognized text was cut to {MaxCharacters} characters")]
    public static partial void TextCut(this ILogger logger, int maxCharacters);

    [LoggerMessage(EventId = 3304, Level = LogLevel.Warning, Message = "Could not delete the OCR working folder ({ErrorType}); only space is lost")]
    public static partial void CleanupFailed(this ILogger logger, string errorType);
}
