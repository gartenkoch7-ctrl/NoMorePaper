using System.Diagnostics;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Processing;
using NoMorePaper.Infrastructure.Processing;
using NoMorePaper.Worker.Telemetry;

namespace NoMorePaper.Worker.Processing;

/// <summary>
/// Works through the processing queue: claims a due job and runs the <see cref="DocumentPipeline"/>
/// in a working folder of its own. A failed attempt is recorded with a technical reason only
/// (exception type, tool and exit code) and retried by the <see cref="RetryPolicy"/> unless the
/// failure is permanent; the loop itself never stops on errors. On shutdown the job is handed back.
/// </summary>
internal sealed class DocumentProcessor(
    IServiceScopeFactory scopes,
    DocumentPipeline pipeline,
    WorkingFolder workingFolder,
    TimeProvider time,
    ILogger<DocumentProcessor> logger) : BackgroundService
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    /// <summary>Longer than the OCR time limit (10 min) plus storing; a crashed Worker's job runs again after it.</summary>
    public static readonly TimeSpan LockFor = TimeSpan.FromMinutes(15);

    // Handing a job back must not wait for ever when the database is gone; the host stops soon after.
    private static readonly TimeSpan s_releaseTimeout = TimeSpan.FromSeconds(5);

    private bool _claimFailing;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        workingFolder.Prepare();
        logger.ProcessingStarted((int)PollInterval.TotalSeconds);
        using var timer = new PeriodicTimer(PollInterval, time);
        do
        {
            // Work through everything that is due, then wait for the next tick.
            while (await ProcessNextAsync(stoppingToken).ConfigureAwait(false))
            {
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    /// <summary>Claims and processes the next due job, if any.</summary>
    /// <returns><c>true</c> when a job was processed (successfully or not), so the next one may be due already.</returns>
    /// <exception cref="OperationCanceledException">The Worker is stopping; the job was handed back.</exception>
    internal async Task<bool> ProcessNextAsync(CancellationToken stoppingToken)
    {
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var queue = scope.ServiceProvider.GetRequiredService<JobQueue>();
            JobClaim? claim;
            try
            {
                claim = await queue.ClaimNextAsync(LockFor, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                // Log the first failure of an outage only; the heartbeat health check reports its duration.
                if (!_claimFailing)
                {
                    logger.ClaimFailed(exception.GetType().Name);
                }

                _claimFailing = true;
                return false;
            }

            if (_claimFailing)
            {
                logger.ClaimRecovered();
                _claimFailing = false;
            }

            if (claim is null)
            {
                return false;
            }

            await ProcessAsync(queue, claim, stoppingToken).ConfigureAwait(false);
            return true;
        }
    }

    private async Task ProcessAsync(JobQueue queue, JobClaim claim, CancellationToken stoppingToken)
    {
        using var activity = WorkerTelemetry.Source.StartActivity("nomorepaper.processing.document");
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
            return;
        }

        var work = workingFolder.CreateAttemptFolder();
        try
        {
            try
            {
                var result = await pipeline.RunAsync(queue, claim, work.FullName, stoppingToken).ConfigureAwait(false);
                if (result is null)
                {
                    logger.ClaimLost(claim.Attempt, id);
                    return;
                }

                var elapsed = time.GetElapsedTime(started);
                ProcessingLog.Completed.Add(1);
                ProcessingLog.Duration.Record(elapsed.TotalSeconds, new KeyValuePair<string, object?>(ProcessingLog.OutcomeTag, "done"));
                logger.DocumentDone(id, (long)elapsed.TotalMilliseconds, result.PageCount, result.Text.Length);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                activity?.SetStatus(ActivityStatusCode.Error);
                await RecordFailureAsync(queue, claim, TechnicalReason(exception), IsPermanent(exception), started, stoppingToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (stoppingToken.IsCancellationRequested)
        {
            // Whatever failed while stopping (often a tool or the database cut off by the stop
            // itself) is no real failure of the document: hand the job back instead of recording it.
            await ReleaseAsync(claim).ConfigureAwait(false);
            if (exception is OperationCanceledException)
            {
                throw;
            }

            throw new OperationCanceledException("The Worker stopped during the attempt.", exception, stoppingToken);
        }
        finally
        {
            DeleteWorkingFolder(id, work);
        }
    }

    // A fresh scope: the interrupted attempt's database context may be in the middle of a command.
    private async Task ReleaseAsync(JobClaim claim)
    {
        try
        {
            using var timeout = new CancellationTokenSource(s_releaseTimeout, time);
            var scope = scopes.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                var queue = scope.ServiceProvider.GetRequiredService<JobQueue>();
                if (await queue.ReleaseAsync(claim, timeout.Token).ConfigureAwait(false))
                {
                    logger.DocumentInterrupted(claim.DocumentId);
                }
                else
                {
                    logger.ClaimLost(claim.Attempt, claim.DocumentId);
                }
            }
        }
        catch (Exception exception)
        {
            logger.ReleaseFailed(claim.DocumentId, exception.GetType().Name);
        }
    }

    private async Task RecordFailureAsync(
        JobQueue queue, JobClaim claim, string reason, bool permanent, long started, CancellationToken stoppingToken)
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

    private void DeleteWorkingFolder(Guid id, DirectoryInfo work)
    {
        try
        {
            work.Delete(recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.CleanupFailed(id, exception.GetType().Name);
        }
    }
}
