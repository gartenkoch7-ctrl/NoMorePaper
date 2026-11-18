using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Telemetry;

namespace NoMorePaper.Application.Processing;

/// <summary>
/// Log events and instruments of document processing (docs/kb/telemetrie.md). The ids 2101–2111
/// come from the Worker range, where processing lived in M1, and stay stable. Only ids, attempts,
/// counts, durations and technical reasons; never titles, file names or text.
/// </summary>
internal static partial class ProcessingLog
{
    /// <summary>Tag on the instruments: <c>done</c>, <c>retry</c> or <c>failed</c>.</summary>
    public const string OutcomeTag = "nomorepaper.processing.outcome";

    public static readonly Counter<long> Completed = ApplicationTelemetry.Meter.CreateCounter<long>(
        "nomorepaper.processing.completed", description: "Documents processed successfully");

    public static readonly Counter<long> Failed = ApplicationTelemetry.Meter.CreateCounter<long>(
        "nomorepaper.processing.failed", description: "Failed processing attempts; outcome retry or failed (given up)");

    public static readonly Histogram<double> Duration = ApplicationTelemetry.Meter.CreateHistogram<double>(
        "nomorepaper.processing.duration", unit: "s", description: "Duration of one processing attempt");

    [LoggerMessage(EventId = 2101, Level = LogLevel.Information, Message = "Processing document {DocumentId}, attempt {Attempt}")]
    public static partial void DocumentStarted(this ILogger logger, Guid documentId, int attempt);

    [LoggerMessage(EventId = 2102, Level = LogLevel.Information, Message = "Document {DocumentId} done after {ElapsedMs} ms ({PageCount} pages, {Characters} characters recognized)")]
    public static partial void DocumentDone(this ILogger logger, Guid documentId, long elapsedMs, int pageCount, int characters);

    [LoggerMessage(EventId = 2103, Level = LogLevel.Warning, Message = "Attempt {Attempt} for document {DocumentId} failed ({Reason}); next attempt at {NextAttemptAt:O}")]
    public static partial void AttemptFailed(this ILogger logger, int attempt, Guid documentId, string reason, DateTimeOffset nextAttemptAt);

    [LoggerMessage(EventId = 2104, Level = LogLevel.Error, Message = "Document {DocumentId} failed for good after attempt {Attempt} ({Reason})")]
    public static partial void DocumentFailed(this ILogger logger, Guid documentId, int attempt, string reason);

    [LoggerMessage(EventId = 2105, Level = LogLevel.Warning, Message = "Could not claim a job ({ErrorType}); retrying on the next tick")]
    public static partial void ClaimFailed(this ILogger logger, string errorType);

    [LoggerMessage(EventId = 2106, Level = LogLevel.Information, Message = "Claiming jobs works again")]
    public static partial void ClaimRecovered(this ILogger logger);

    [LoggerMessage(EventId = 2107, Level = LogLevel.Error, Message = "Could not record the failure of document {DocumentId} ({ErrorType}); the job runs again when its lock expires")]
    public static partial void FailureNotRecorded(this ILogger logger, Guid documentId, string errorType);

    [LoggerMessage(EventId = 2108, Level = LogLevel.Information, Message = "Processing of document {DocumentId} was interrupted by shutdown; the job was handed back and runs again on the next start")]
    public static partial void DocumentInterrupted(this ILogger logger, Guid documentId);

    [LoggerMessage(EventId = 2109, Level = LogLevel.Warning, Message = "Could not delete the working folder of document {DocumentId} ({ErrorType}); a Worker start a day later removes it")]
    public static partial void CleanupFailed(this ILogger logger, Guid documentId, string errorType);

    [LoggerMessage(EventId = 2110, Level = LogLevel.Warning, Message = "Attempt {Attempt} for document {DocumentId} lost its claim to another Worker; its result is discarded")]
    public static partial void ClaimLost(this ILogger logger, int attempt, Guid documentId);

    [LoggerMessage(EventId = 2111, Level = LogLevel.Warning, Message = "Could not hand back document {DocumentId} on shutdown ({ErrorType}); the job runs again when its lock expires")]
    public static partial void ReleaseFailed(this ILogger logger, Guid documentId, string errorType);
}
