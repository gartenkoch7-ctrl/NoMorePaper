using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Telemetry;

namespace NoMorePaper.Application.Separation;

/// <summary>
/// Log events and instruments of the stack separation (Application 5700–5799, docs/kb/telemetrie.md).
/// Only ids, attempts, counts, durations and technical reasons; never page text or senders.
/// </summary>
internal static partial class SeparationLog
{
    /// <summary>Tag on <see cref="Completed"/>: <c>suggested</c>, <c>none</c> or <c>skipped</c>.</summary>
    public const string OutcomeTag = "nomorepaper.separation.outcome";

    public static readonly Counter<long> Completed = ApplicationTelemetry.Meter.CreateCounter<long>(
        "nomorepaper.separation.completed", description: "Scanned stacks whose separation stage ended; outcome suggested, none or skipped");

    public static readonly Histogram<double> Duration = ApplicationTelemetry.Meter.CreateHistogram<double>(
        "nomorepaper.separation.duration", unit: "s", description: "Duration of one separation attempt");

    [LoggerMessage(EventId = 5700, Level = LogLevel.Information, Message = "Looking for documents in the stack {DocumentId}, attempt {Attempt}")]
    public static partial void SeparationStarted(this ILogger logger, Guid documentId, int attempt);

    [LoggerMessage(EventId = 5701, Level = LogLevel.Information, Message = "Stack {DocumentId} probably holds {Parts} documents ({BlankPages} blank pages), found after {ElapsedMs} ms")]
    public static partial void SplitSuggested(this ILogger logger, Guid documentId, int parts, int blankPages, long elapsedMs);

    [LoggerMessage(EventId = 5702, Level = LogLevel.Information, Message = "Stack {DocumentId} ({Pages} pages) is one document, found after {ElapsedMs} ms")]
    public static partial void NoSplitFound(this ILogger logger, Guid documentId, int pages, long elapsedMs);

    [LoggerMessage(EventId = 5703, Level = LogLevel.Warning, Message = "The local LLM gave no answer for stack {DocumentId} ({ErrorType}); only the page signs count")]
    public static partial void AssessmentFailed(this ILogger logger, Guid documentId, string errorType);

    [LoggerMessage(EventId = 5704, Level = LogLevel.Warning, Message = "The local LLM stayed busy; separation of {DocumentId} waits for the next claim (handed back: {Released})")]
    public static partial void LlmBusy(this ILogger logger, Guid documentId, bool released);

    [LoggerMessage(EventId = 5705, Level = LogLevel.Warning, Message = "Separation attempt {Attempt} for {DocumentId} failed ({Reason}); next attempt at {NextAttemptAt:O}")]
    public static partial void AttemptFailed(this ILogger logger, int attempt, Guid documentId, string reason, DateTimeOffset nextAttemptAt);

    [LoggerMessage(EventId = 5706, Level = LogLevel.Warning, Message = "Separation of {DocumentId} skipped after attempt {Attempt} ({Reason}); recognition follows without suggestion")]
    public static partial void SeparationSkipped(this ILogger logger, Guid documentId, int attempt, string reason);

    [LoggerMessage(EventId = 5707, Level = LogLevel.Warning, Message = "Separation attempt {Attempt} for {DocumentId} lost its claim; its result is discarded")]
    public static partial void ClaimLost(this ILogger logger, int attempt, Guid documentId);

    [LoggerMessage(EventId = 5708, Level = LogLevel.Error, Message = "The outcome of separating {DocumentId} could not be recorded ({ErrorType}); the job runs again when its lock expires")]
    public static partial void OutcomeNotRecorded(this ILogger logger, Guid documentId, string errorType);

    [LoggerMessage(EventId = 5710, Level = LogLevel.Information, Message = "Account {AccountId} accepted the split suggestion of {DocumentId} ({Parts} documents)")]
    public static partial void SplitAccepted(this ILogger logger, Guid accountId, Guid documentId, int parts);

    [LoggerMessage(EventId = 5711, Level = LogLevel.Information, Message = "Account {AccountId} dismissed the split suggestion of {DocumentId}")]
    public static partial void SplitDismissed(this ILogger logger, Guid accountId, Guid documentId);
}
