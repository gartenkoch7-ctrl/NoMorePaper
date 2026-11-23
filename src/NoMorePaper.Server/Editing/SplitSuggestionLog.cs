using NoMorePaper.Application.Classification;

namespace NoMorePaper.Server.Editing;

/// <summary>Log events of the answers to split suggestions (Server 1650–1669, docs/kb/telemetrie.md). Ids and fixed words only.</summary>
internal static partial class SplitSuggestionLog
{
    [LoggerMessage(EventId = 1650, Level = LogLevel.Information, Message = "Split suggestion {Action} on document {DocumentId}: {Outcome}")]
    public static partial void SplitSuggestionAnswered(this ILogger logger, string action, Guid documentId, ChangeOutcome outcome);
}
