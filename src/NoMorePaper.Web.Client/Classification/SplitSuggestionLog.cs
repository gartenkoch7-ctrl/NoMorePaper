using Microsoft.Extensions.Logging;

namespace NoMorePaper.Web.Client.Classification;

/// <summary>
/// Log events of the split suggestion in "Zu prüfen" (EventId range 4900–4903, see docs/kb/telemetrie.md).
/// Only counts and fixed words, never titles or page texts.
/// </summary>
internal static partial class SplitSuggestionLog
{
    [LoggerMessage(EventId = 4900, EventName = "SplitSuggestionShown", Level = LogLevel.Debug,
        Message = "Split suggestion shown with {Parts} parts and {BlankPages} blank pages")]
    public static partial void SplitSuggestionShown(this ILogger logger, int parts, int blankPages);

    [LoggerMessage(EventId = 4901, EventName = "SplitSuggestionAccepted", Level = LogLevel.Information,
        Message = "Split suggestion accepted in the review list")]
    public static partial void SplitSuggestionAccepted(this ILogger logger);

    [LoggerMessage(EventId = 4902, EventName = "SplitSuggestionDismissed", Level = LogLevel.Information,
        Message = "Split suggestion dismissed in the review list")]
    public static partial void SplitSuggestionDismissed(this ILogger logger);

    /// <param name="code">A fixed word: <c>accept</c> or <c>dismiss</c>.</param>
    [LoggerMessage(EventId = 4903, EventName = "SplitSuggestionAnswerFailed", Level = LogLevel.Warning,
        Message = "Answering the split suggestion failed ({Code})")]
    public static partial void SplitSuggestionAnswerFailed(this ILogger logger, string code);
}
