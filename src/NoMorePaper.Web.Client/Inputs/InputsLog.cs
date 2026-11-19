using Microsoft.Extensions.Logging;

namespace NoMorePaper.Web.Client.Inputs;

/// <summary>
/// Log events of the input clients (EventId range 4700–4799, see docs/kb/telemetrie.md); 4700–4789 are
/// for the pages. Only operations, HTTP status codes and exception types, never addresses or names.
/// </summary>
internal static partial class InputsLog
{
    [LoggerMessage(EventId = 4790, EventName = "InputsCallFailed", Level = LogLevel.Warning,
        Message = "Inputs call {Operation} failed with HTTP status {StatusCode}")]
    public static partial void InputsCallFailed(this ILogger logger, string operation, int statusCode);

    [LoggerMessage(EventId = 4791, EventName = "InputsUnreachable", Level = LogLevel.Warning,
        Message = "Inputs call {Operation} failed: {ExceptionType}")]
    public static partial void InputsUnreachable(this ILogger logger, string operation, string exceptionType);
}
