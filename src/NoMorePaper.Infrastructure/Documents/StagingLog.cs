using Microsoft.Extensions.Logging;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Infrastructure.Documents;

/// <summary>Log events of the files an edit stages (Infrastructure 3978, docs/kb/telemetrie.md). Ids and kinds only.</summary>
internal static partial class StagingLog
{
    [LoggerMessage(EventId = 3978, Level = LogLevel.Information, Message = "Published the new {Kind} of document {DocumentId} after its edit was stored")]
    public static partial void StagedPublished(this ILogger logger, DocumentFileKind kind, Guid documentId);
}
