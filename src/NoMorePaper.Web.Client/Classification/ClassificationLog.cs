using Microsoft.Extensions.Logging;

namespace NoMorePaper.Web.Client.Classification;

/// <summary>
/// Log events of the classification pages (EventId range 4500–4599, see docs/kb/telemetrie.md).
/// Only counts, list kinds and HTTP status codes are logged, never titles, names, amounts or summaries.
/// </summary>
internal static partial class ClassificationLog
{
    [LoggerMessage(EventId = 4500, EventName = "ClassificationCallFailed", Level = LogLevel.Warning,
        Message = "Classification call {Operation} failed with HTTP status {StatusCode}")]
    public static partial void ClassificationCallFailed(this ILogger logger, string operation, int statusCode);

    [LoggerMessage(EventId = 4501, EventName = "ClassificationUnreachable", Level = LogLevel.Warning,
        Message = "Classification call {Operation} failed: {ExceptionType}")]
    public static partial void ClassificationUnreachable(this ILogger logger, string operation, string exceptionType);

    [LoggerMessage(EventId = 4502, EventName = "FieldsLoaded", Level = LogLevel.Debug,
        Message = "Fields of a document loaded (recognition {Recognition})")]
    public static partial void FieldsLoaded(this ILogger logger, string recognition);

    [LoggerMessage(EventId = 4503, EventName = "FieldsSaved", Level = LogLevel.Information,
        Message = "Fields of a document edited and confirmed")]
    public static partial void FieldsSaved(this ILogger logger);

    [LoggerMessage(EventId = 4504, EventName = "FieldsConfirmed", Level = LogLevel.Information,
        Message = "Fields of a document confirmed")]
    public static partial void FieldsConfirmed(this ILogger logger);

    [LoggerMessage(EventId = 4505, EventName = "EditorsChanged", Level = LogLevel.Information,
        Message = "Editors of a document changed: {Count} selected")]
    public static partial void EditorsChanged(this ILogger logger, int count);

    [LoggerMessage(EventId = 4506, EventName = "ReviewLoaded", Level = LogLevel.Debug,
        Message = "List to review loaded: {Count} entries")]
    public static partial void ReviewLoaded(this ILogger logger, int count);

    [LoggerMessage(EventId = 4507, EventName = "DuplicateKept", Level = LogLevel.Information,
        Message = "A possible duplicate was kept")]
    public static partial void DuplicateKept(this ILogger logger);

    [LoggerMessage(EventId = 4508, EventName = "DuplicateDeleted", Level = LogLevel.Information,
        Message = "A possible duplicate was deleted")]
    public static partial void DuplicateDeleted(this ILogger logger);

    [LoggerMessage(EventId = 4509, EventName = "EntryCreated", Level = LogLevel.Information,
        Message = "A list entry was created in {Kind}")]
    public static partial void EntryCreated(this ILogger logger, EntryKind kind);

    [LoggerMessage(EventId = 4510, EventName = "EntryRenamed", Level = LogLevel.Information,
        Message = "A list entry was renamed in {Kind}")]
    public static partial void EntryRenamed(this ILogger logger, EntryKind kind);

    [LoggerMessage(EventId = 4511, EventName = "EntriesMerged", Level = LogLevel.Information,
        Message = "{Count} list entries were merged in {Kind}")]
    public static partial void EntriesMerged(this ILogger logger, EntryKind kind, int count);

    [LoggerMessage(EventId = 4512, EventName = "FilterApplied", Level = LogLevel.Information,
        Message = "Document filter applied: {ActiveFilters} filters, {Count} documents")]
    public static partial void FilterApplied(this ILogger logger, int activeFilters, int count);

    [LoggerMessage(EventId = 4513, EventName = "UploadDuplicateShown", Level = LogLevel.Information,
        Message = "An upload was refused because the file exists already")]
    public static partial void UploadDuplicateShown(this ILogger logger);

    [LoggerMessage(EventId = 4514, EventName = "ReviewCountUnavailable", Level = LogLevel.Debug,
        Message = "The number of documents to review could not be loaded")]
    public static partial void ReviewCountUnavailable(this ILogger logger);

    [LoggerMessage(EventId = 4515, EventName = "FieldsChangedMeanwhile", Level = LogLevel.Information,
        Message = "Saving or confirming the fields was refused because they changed meanwhile; they were loaded again")]
    public static partial void FieldsChangedMeanwhile(this ILogger logger);
}
