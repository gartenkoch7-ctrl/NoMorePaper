namespace NoMorePaper.Server.Documents;

/// <summary>
/// Log events of the document API (Server 1100–1119, docs/kb/telemetrie.md). Reasons are fixed
/// words; file names and content never appear.
/// </summary>
internal static partial class DocumentsLog
{
    [LoggerMessage(EventId = 1100, Level = LogLevel.Information, Message = "Upload accepted as document {DocumentId} ({SizeBytes} bytes, {MediaType})")]
    public static partial void UploadAccepted(this ILogger logger, Guid documentId, long sizeBytes, string mediaType);

    [LoggerMessage(EventId = 1101, Level = LogLevel.Warning, Message = "Upload rejected: {Reason} ({SizeBytes} bytes)")]
    public static partial void UploadRejected(this ILogger logger, string reason, long sizeBytes);
}
