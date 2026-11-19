using Microsoft.Extensions.Logging;

namespace NoMorePaper.Web.Client.Inputs;

/// <summary>
/// Log events of the input clients (EventId range 4700–4799, see docs/kb/telemetrie.md); 4700–4789 are
/// for the pages. Only operations, HTTP status codes and exception types, never addresses or names.
/// </summary>
internal static partial class InputsLog
{
    [LoggerMessage(EventId = 4700, EventName = "InputsPageLoaded", Level = LogLevel.Information,
        Message = "Inputs page loaded with {Count} rows")]
    public static partial void InputsPageLoaded(this ILogger logger, int count);

    [LoggerMessage(EventId = 4701, EventName = "PreferencesSaved", Level = LogLevel.Information,
        Message = "Preferences saved ({Count} mail senders)")]
    public static partial void PreferencesSaved(this ILogger logger, int count);

    /// <param name="code">A fixed word for what failed: <c>load-inputs</c>, <c>load-preferences</c>, <c>save-preferences</c>, <c>owner-check</c>, <c>hand-over</c>.</param>
    [LoggerMessage(EventId = 4702, EventName = "InputsPageFailed", Level = LogLevel.Warning,
        Message = "Inputs page action failed: {Code}")]
    public static partial void InputsPageFailed(this ILogger logger, string code);

    [LoggerMessage(EventId = 4703, EventName = "DocumentHandedOver", Level = LogLevel.Information,
        Message = "Document of unknown origin handed over")]
    public static partial void DocumentHandedOver(this ILogger logger);

    [LoggerMessage(EventId = 4704, EventName = "OwnerCheckKept", Level = LogLevel.Information,
        Message = "Document of unknown origin kept by its owner")]
    public static partial void OwnerCheckKept(this ILogger logger);

    [LoggerMessage(EventId = 4790, EventName = "InputsCallFailed", Level = LogLevel.Warning,
        Message = "Inputs call {Operation} failed with HTTP status {StatusCode}")]
    public static partial void InputsCallFailed(this ILogger logger, string operation, int statusCode);

    [LoggerMessage(EventId = 4791, EventName = "InputsUnreachable", Level = LogLevel.Warning,
        Message = "Inputs call {Operation} failed: {ExceptionType}")]
    public static partial void InputsUnreachable(this ILogger logger, string operation, string exceptionType);
}
