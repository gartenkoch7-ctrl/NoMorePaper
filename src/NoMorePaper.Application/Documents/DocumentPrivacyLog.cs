using Microsoft.Extensions.Logging;

namespace NoMorePaper.Application.Documents;

/// <summary>Log events of document ownership (Application 5120–5129, docs/kb/telemetrie.md). Ids only.</summary>
internal static partial class DocumentPrivacyLog
{
    [LoggerMessage(EventId = 5120, Level = LogLevel.Information, Message = "Document {DocumentId} set to private {IsPrivate} by its owner {AccountId}")]
    public static partial void PrivacyChanged(this ILogger logger, Guid documentId, bool isPrivate, Guid accountId);
}
