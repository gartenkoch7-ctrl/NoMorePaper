using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Telemetry;
using NoMorePaper.Domain.Processing;

namespace NoMorePaper.Application.Recognition;

/// <summary>
/// The recognition stage of a claimed job (M3 plan, task 2): runs a <see cref="RecognitionAttempt"/> and
/// records its outcome. A failed attempt is retried like OCR (<see cref="RetryPolicy"/>: after 1, 5 and
/// 30 minutes); after the last one the job is removed and the document stays done without fields. A
/// confirmed document is never recognized again.
/// </summary>
/// <remarks>
/// Lives in the scope of one job (lists and examples come from the database context of that scope).
/// Exceptions while the Worker stops are passed on, so the caller hands the job back.
/// </remarks>
public sealed class RecognizeDocument(
    IClassificationStore lists,
    IRecognitionExamples examples,
    IRecognizer recognizer,
    TimeProvider time,
    ILogger<RecognizeDocument> logger)
{
    private readonly RecognitionAttempt _attempt = new(lists, examples, recognizer, time, logger);

    /// <exception cref="OperationCanceledException">The Worker is stopping; the caller hands the job back.</exception>
    public async Task RunAsync(IJobQueue queue, JobClaim claim, CancellationToken stoppingToken)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(claim);
        using var activity = ApplicationTelemetry.Source.StartActivity("nomorepaper.recognition.document");
        activity?.SetTag("nomorepaper.recognition.attempt", claim.Attempt);
        var started = time.GetTimestamp();
        var id = claim.DocumentId;
        logger.RecognitionStarted(id, claim.Attempt);

        if (claim.Attempt > RetryPolicy.MaxAttempts)
        {
            // Only possible when the Worker was killed during every attempt (crash, out of memory).
            activity?.SetStatus(ActivityStatusCode.Error);
            await RecordFailureAsync(queue, claim, "Recognition was interrupted in every attempt", started, stoppingToken)
                .ConfigureAwait(false);
            return;
        }

        try
        {
            var outcome = await _attempt.RunAsync(queue, claim, stoppingToken).ConfigureAwait(false);
            Report(claim, outcome, started);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            await RecordFailureAsync(queue, claim, TechnicalReason(exception), started, stoppingToken).ConfigureAwait(false);
        }
    }

    private void Report(JobClaim claim, RecognitionOutcome outcome, long started)
    {
        var id = claim.DocumentId;
        if (outcome.Commit == RecognitionCommit.ClaimLost)
        {
            logger.ClaimLost(claim.Attempt, id);
            return;
        }

        var elapsed = time.GetElapsedTime(started);
        var recognized = outcome.Commit == RecognitionCommit.Classified;
        var tag = new KeyValuePair<string, object?>(RecognitionLog.OutcomeTag, recognized ? "recognized" : "skipped");
        RecognitionLog.Completed.Add(1, tag);
        RecognitionLog.Duration.Record(elapsed.TotalSeconds, tag);
        if (recognized)
        {
            logger.DocumentRecognized(id, (long)elapsed.TotalMilliseconds, outcome.ExampleCount, outcome.RejectedCount);
        }
        else if (outcome.SkipReason is { } reason)
        {
            logger.RecognitionSkipped(id, reason);
        }
        else
        {
            logger.ConfirmedMeanwhile(id);
        }
    }

    private async Task RecordFailureAsync(IJobQueue queue, JobClaim claim, string reason, long started, CancellationToken stoppingToken)
    {
        var id = claim.DocumentId;
        try
        {
            // Recognition failures are never permanent: the model or its service may work again later.
            var recorded = await queue.FailAsync(claim, reason, permanent: false, time.GetUtcNow(), stoppingToken).ConfigureAwait(false);
            if (recorded.Outcome == FailureOutcome.ClaimLost)
            {
                logger.ClaimLost(claim.Attempt, id);
                return;
            }

            var tag = new KeyValuePair<string, object?>(
                RecognitionLog.OutcomeTag, recorded.Outcome == FailureOutcome.GivenUp ? "given_up" : "retry");
            RecognitionLog.Failed.Add(1, tag);
            RecognitionLog.Duration.Record(time.GetElapsedTime(started).TotalSeconds, tag);
            if (recorded.NextAttemptAt is { } nextAttemptAt)
            {
                logger.AttemptFailed(claim.Attempt, id, reason, nextAttemptAt);
            }
            else
            {
                logger.RecognitionGivenUp(id, claim.Attempt, reason);
            }
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.FailureNotRecorded(id, exception.GetType().Name);
        }
    }

    // Exception messages may quote the model's answer or name hosts, so only the type is kept; the
    // recognizer's own exception carries a fixed reason (e.g. an HTTP status code).
    private static string TechnicalReason(Exception exception) => exception switch
    {
        RecognitionException recognition => $"{nameof(RecognitionException)}: {recognition.Message}",
        _ => exception.GetType().Name,
    };
}
