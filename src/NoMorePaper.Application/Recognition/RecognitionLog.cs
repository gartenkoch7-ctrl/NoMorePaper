using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Telemetry;

namespace NoMorePaper.Application.Recognition;

/// <summary>
/// Log events and instruments of recognition, content duplicates and the backlog (Application
/// 5200–5249, docs/kb/telemetrie.md). Only ids, attempts, counts, durations, fixed field names and
/// technical reasons; never document text, names, titles or summaries.
/// </summary>
internal static partial class RecognitionLog
{
    /// <summary>Tag on the instruments: <c>recognized</c>, <c>skipped</c>, <c>retry</c> or <c>given_up</c>.</summary>
    public const string OutcomeTag = "nomorepaper.recognition.outcome";

    /// <summary>Tag on <see cref="RejectedValues"/>: the field (<see cref="RecognitionChecks.Correspondent"/>, …).</summary>
    public const string FieldTag = "nomorepaper.recognition.field";

    public static readonly Counter<long> Completed = ApplicationTelemetry.Meter.CreateCounter<long>(
        "nomorepaper.recognition.completed", description: "Documents whose recognition stage ended; outcome recognized or skipped");

    public static readonly Counter<long> Failed = ApplicationTelemetry.Meter.CreateCounter<long>(
        "nomorepaper.recognition.failed", description: "Failed recognition attempts; outcome retry or given_up");

    public static readonly Histogram<double> Duration = ApplicationTelemetry.Meter.CreateHistogram<double>(
        "nomorepaper.recognition.duration", unit: "s", description: "Duration of one recognition attempt");

    public static readonly Counter<long> RejectedValues = ApplicationTelemetry.Meter.CreateCounter<long>(
        "nomorepaper.recognition.rejected_values", description: "Recognized values dropped by the checks against the text");

    public static readonly Counter<long> Duplicates = ApplicationTelemetry.Meter.CreateCounter<long>(
        "nomorepaper.recognition.duplicates", description: "Documents marked as possible duplicates by their content");

    [LoggerMessage(EventId = 5200, Level = LogLevel.Information, Message = "Recognizing document {DocumentId}, attempt {Attempt}")]
    public static partial void RecognitionStarted(this ILogger logger, Guid documentId, int attempt);

    [LoggerMessage(EventId = 5201, Level = LogLevel.Information, Message = "Document {DocumentId} recognized after {ElapsedMs} ms ({ExampleCount} examples, {RejectedCount} values dropped by the checks)")]
    public static partial void DocumentRecognized(this ILogger logger, Guid documentId, long elapsedMs, int exampleCount, int rejectedCount);

    [LoggerMessage(EventId = 5202, Level = LogLevel.Information, Message = "Recognition of document {DocumentId} skipped ({Reason})")]
    public static partial void RecognitionSkipped(this ILogger logger, Guid documentId, string reason);

    [LoggerMessage(EventId = 5203, Level = LogLevel.Warning, Message = "Recognition attempt {Attempt} for document {DocumentId} failed ({Reason}); next attempt at {NextAttemptAt:O}")]
    public static partial void AttemptFailed(this ILogger logger, int attempt, Guid documentId, string reason, DateTimeOffset nextAttemptAt);

    [LoggerMessage(EventId = 5204, Level = LogLevel.Warning, Message = "Recognition of document {DocumentId} given up after attempt {Attempt} ({Reason}); the document stays without fields")]
    public static partial void RecognitionGivenUp(this ILogger logger, Guid documentId, int attempt, string reason);

    [LoggerMessage(EventId = 5205, Level = LogLevel.Information, Message = "Document {DocumentId} was confirmed during recognition; the result is discarded")]
    public static partial void ConfirmedMeanwhile(this ILogger logger, Guid documentId);

    [LoggerMessage(EventId = 5206, Level = LogLevel.Debug, Message = "Recognized {Field} of document {DocumentId} dropped: the text does not back it")]
    public static partial void ValueRejected(this ILogger logger, string field, Guid documentId);

    [LoggerMessage(EventId = 5207, Level = LogLevel.Information, Message = "Document {DocumentId} marked as possible duplicate of document {OtherId}")]
    public static partial void PossibleDuplicate(this ILogger logger, Guid documentId, Guid otherId);

    [LoggerMessage(EventId = 5208, Level = LogLevel.Warning, Message = "Could not check document {DocumentId} for content duplicates ({ErrorType}); it stays without hint")]
    public static partial void DuplicateCheckFailed(this ILogger logger, Guid documentId, string errorType);

    [LoggerMessage(EventId = 5209, Level = LogLevel.Information, Message = "Queued recognition for {Count} documents of the backlog")]
    public static partial void BacklogQueued(this ILogger logger, int count);

    [LoggerMessage(EventId = 5210, Level = LogLevel.Warning, Message = "Recognition attempt {Attempt} for document {DocumentId} lost its job (taken over by another Worker, or the document was deleted); its result is discarded")]
    public static partial void ClaimLost(this ILogger logger, int attempt, Guid documentId);

    [LoggerMessage(EventId = 5211, Level = LogLevel.Error, Message = "Could not record the failed recognition of document {DocumentId} ({ErrorType}); the job runs again when its lock expires")]
    public static partial void FailureNotRecorded(this ILogger logger, Guid documentId, string errorType);
}
