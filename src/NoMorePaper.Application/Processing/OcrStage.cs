using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Telemetry;
using NoMorePaper.Domain.Processing;

namespace NoMorePaper.Application.Processing;

/// <summary>
/// The OCR stage of a claimed job: runs a <see cref="ProcessingAttempt"/> in a working folder of its own
/// and records its outcome. A failed attempt is recorded with a technical reason only (exception type,
/// tool and exit code) and retried by the <see cref="RetryPolicy"/> unless the failure is permanent.
/// Exceptions while the Worker stops are passed on, so the caller hands the job back.
/// </summary>
internal sealed class OcrStage(IDocumentStorage storage, IOcrEngine ocr, IWorkingFolder workingFolder, TimeProvider time, ILogger logger)
{
    private readonly ProcessingAttempt _attempt = new(storage, ocr, workingFolder);

    /// <returns>The recognized text when the stage is complete, otherwise <c>null</c>.</returns>
    public async Task<string?> RunAsync(IJobQueue queue, JobClaim claim, CancellationToken stoppingToken)
    {
        using var activity = ApplicationTelemetry.Source.StartActivity("nomorepaper.processing.document");
        activity?.SetTag("nomorepaper.processing.attempt", claim.Attempt);
        var started = time.GetTimestamp();
        var id = claim.DocumentId;
        logger.DocumentStarted(id, claim.Attempt);

        if (claim.Attempt > RetryPolicy.MaxAttempts)
        {
            // Only possible when the Worker was killed during every attempt (crash, out of memory).
            activity?.SetStatus(ActivityStatusCode.Error);
            await RecordFailureAsync(queue, claim, "Processing was interrupted in every attempt", permanent: false, started, stoppingToken)
                .ConfigureAwait(false);
            return null;
        }

        var work = workingFolder.CreateAttemptFolder();
        try
        {
            var result = await _attempt.RunAsync(queue, claim, work, stoppingToken).ConfigureAwait(false);
            if (result is null)
            {
                logger.ClaimLost(claim.Attempt, id);
                return null;
            }

            var elapsed = time.GetElapsedTime(started);
            ProcessingLog.Completed.Add(1);
            ProcessingLog.Duration.Record(elapsed.TotalSeconds, new KeyValuePair<string, object?>(ProcessingLog.OutcomeTag, "done"));
            logger.DocumentDone(id, (long)elapsed.TotalMilliseconds, result.PageCount, result.Text.Length);
            return result.Text;
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            await RecordFailureAsync(queue, claim, TechnicalReason(exception), IsPermanent(exception), started, stoppingToken)
                .ConfigureAwait(false);
            return null;
        }
        finally
        {
            DeleteWorkingFolder(id, work);
        }
    }

    private async Task RecordFailureAsync(
        IJobQueue queue, JobClaim claim, string reason, bool permanent, long started, CancellationToken stoppingToken)
    {
        var id = claim.DocumentId;
        try
        {
            var recorded = await queue.FailAsync(claim, reason, permanent, time.GetUtcNow(), stoppingToken).ConfigureAwait(false);
            if (recorded.Outcome == FailureOutcome.ClaimLost)
            {
                logger.ClaimLost(claim.Attempt, id);
                return;
            }

            var outcome = recorded.Outcome == FailureOutcome.GivenUp ? "failed" : "retry";
            ProcessingLog.Failed.Add(1, new KeyValuePair<string, object?>(ProcessingLog.OutcomeTag, outcome));
            ProcessingLog.Duration.Record(
                time.GetElapsedTime(started).TotalSeconds, new KeyValuePair<string, object?>(ProcessingLog.OutcomeTag, outcome));
            if (recorded.NextAttemptAt is { } nextAttemptAt)
            {
                logger.AttemptFailed(claim.Attempt, id, reason, nextAttemptAt);
            }
            else
            {
                logger.DocumentFailed(id, claim.Attempt, reason);
            }
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.FailureNotRecorded(id, exception.GetType().Name);
        }
    }

    // The document itself cannot be processed: retrying would only repeat the failure.
    private static bool IsPermanent(Exception exception) =>
        exception is ExternalToolException { IsPermanent: true } or NotSupportedException;

    // Exception messages may contain paths or tool output, so only the type (and tool and exit code) is kept.
    private static string TechnicalReason(Exception exception) => exception switch
    {
        ExternalToolException tool => $"{nameof(ExternalToolException)}: {tool.Tool} exited with code {tool.ExitCode}",
        _ => exception.GetType().Name,
    };

    private void DeleteWorkingFolder(Guid id, string work)
    {
        try
        {
            workingFolder.DeleteAttemptFolder(work);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.CleanupFailed(id, exception.GetType().Name);
        }
    }
}
