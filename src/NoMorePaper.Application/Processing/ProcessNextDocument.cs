using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Recognition;
using NoMorePaper.Domain.Processing;

namespace NoMorePaper.Application.Processing;

/// <summary>
/// Claims the next due job and runs its stage with the <see cref="IStageRunner"/> registered for it:
/// OCR (followed by the content duplicate check), recognition, and the stages M4b adds. Each stage
/// records its failures with a technical reason only and retries them by the <see cref="RetryPolicy"/>;
/// a failed attempt or an unreachable queue never stops the caller's loop. On shutdown the job is handed back.
/// </summary>
/// <remarks>
/// One instance per Worker (singleton): it remembers whether claiming fails, to log an outage once.
/// Jobs at the stages that ask the local LLM are claimed only once the model is ready
/// (<see cref="RecognitionReadiness"/>). Each call works in a scope of its own, because the queue holds a
/// database context; the stage runners come from that scope.
/// </remarks>
public sealed class ProcessNextDocument(
    IServiceScopeFactory scopes,
    RecognitionReadiness readiness,
    TimeProvider time,
    ILogger<ProcessNextDocument> logger)
{
    /// <summary>
    /// Longer than the OCR time limit (10 min) plus storing, and than the recognition time limit (5 min by
    /// default); a crashed Worker's job runs again after it.
    /// </summary>
    public static readonly TimeSpan LockFor = TimeSpan.FromMinutes(15);

    // Handing a job back must not wait for ever when the database is gone; the host stops soon after.
    private static readonly TimeSpan s_releaseTimeout = TimeSpan.FromSeconds(5);

    private bool _claimFailing;

    /// <summary>Claims and processes the next due job, if any.</summary>
    /// <returns><c>true</c> when a job was processed (successfully or not), so the next one may be due already.</returns>
    /// <exception cref="OperationCanceledException">The Worker is stopping; the job was handed back.</exception>
    public async Task<bool> ProcessNextAsync(CancellationToken stoppingToken)
    {
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var queue = scope.ServiceProvider.GetRequiredService<IJobQueue>();
            JobClaim? claim;
            try
            {
                claim = await queue.ClaimNextAsync(LockFor, readiness.IsReady, stoppingToken).ConfigureAwait(false);
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

            await RunStageAsync(scope.ServiceProvider, queue, claim, stoppingToken).ConfigureAwait(false);
            return true;
        }
    }

    private async Task RunStageAsync(IServiceProvider services, IJobQueue queue, JobClaim claim, CancellationToken stoppingToken)
    {
        try
        {
            var runner = services.GetKeyedService<IStageRunner>(claim.Stage);
            if (runner is null)
            {
                await FailWithoutRunnerAsync(queue, claim, stoppingToken).ConfigureAwait(false);
                return;
            }

            await runner.RunAsync(queue, claim, stoppingToken).ConfigureAwait(false);
            await DeleteFilesOfRemovedDocumentAsync(services, claim.DocumentId, stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (stoppingToken.IsCancellationRequested)
        {
            // Whatever failed while stopping (often a tool, the model or the database cut off by the
            // stop itself) is no real failure of the document: hand the job back instead of recording it.
            await ReleaseAsync(claim).ConfigureAwait(false);
            if (exception is OperationCanceledException)
            {
                throw;
            }

            throw new OperationCanceledException("The Worker stopped during the attempt.", exception, stoppingToken);
        }
    }

    // A document moved to the trash and removed for good while its attempt ran (M4b) lost its folders before
    // the attempt wrote its files (archive, staged edit), which then lie where nothing points to them. The
    // removal commits before it deletes the folders, and the attempt wrote before it failed to commit, so
    // one of the two deletes always comes last. A failure here only costs space.
    private async Task DeleteFilesOfRemovedDocumentAsync(IServiceProvider services, Guid documentId, CancellationToken stoppingToken)
    {
        try
        {
            if (await services.GetRequiredService<IDocumentRepository>().ExistsAsync(documentId, stoppingToken).ConfigureAwait(false))
            {
                return;
            }

            services.GetRequiredService<IDocumentStorage>().Delete(documentId);
            logger.RemovedDocumentFilesDeleted(documentId);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.RemovedDocumentFilesLeft(documentId, exception.GetType().Name);
        }
    }

    // A stage without a runner (e.g. one a later milestone adds, queued by a newer build) must neither stop
    // the Worker nor block the queue: the attempt fails like any other, so the job waits for its retry
    // delay and the jobs behind it run.
    private async Task FailWithoutRunnerAsync(IJobQueue queue, JobClaim claim, CancellationToken stoppingToken)
    {
        try
        {
            var reason = $"No runner for stage {claim.Stage}";
            var failure = await queue.FailAsync(claim, reason, permanent: false, time.GetUtcNow(), stoppingToken).ConfigureAwait(false);
            logger.StageRunnerMissing(claim.Stage.ToString(), claim.DocumentId, failure.Outcome.ToString());
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.FailureNotRecorded(claim.DocumentId, exception.GetType().Name);
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
                var queue = scope.ServiceProvider.GetRequiredService<IJobQueue>();
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
}
