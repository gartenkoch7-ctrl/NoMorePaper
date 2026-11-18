using System.Diagnostics;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Domain.Processing;
using NoMorePaper.Infrastructure.Processing;
using NoMorePaper.Worker.Telemetry;

namespace NoMorePaper.Worker.Processing;

/// <summary>
/// Works through the processing queue: claims a due job, runs OCR on a working copy of the
/// original, stores the archive PDF, moves the original into the archive and completes the
/// document. A failed attempt is recorded with a technical reason only (exception type, tool and
/// exit code) and retried by the <see cref="RetryPolicy"/>; the loop itself never stops on errors.
/// </summary>
internal sealed class DocumentProcessor(
    IServiceScopeFactory scopes,
    IDocumentStorage storage,
    IOcrEngine ocr,
    TimeProvider time,
    ILogger<DocumentProcessor> logger) : BackgroundService
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    /// <summary>Longer than the OCR time limit (10 min) plus storing; a crashed Worker's job runs again after it.</summary>
    public static readonly TimeSpan LockFor = TimeSpan.FromMinutes(15);

    private bool _claimFailing;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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

    /// <returns><c>true</c> when a job was processed (successfully or not), so the next one may be due already.</returns>
    private async Task<bool> ProcessNextAsync(CancellationToken stoppingToken)
    {
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var queue = scope.ServiceProvider.GetRequiredService<JobQueue>();
            ProcessingJob? job;
            try
            {
                job = await queue.ClaimNextAsync(LockFor, stoppingToken).ConfigureAwait(false);
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

            if (job is null)
            {
                return false;
            }

            await ProcessAsync(queue, job, stoppingToken).ConfigureAwait(false);
            return true;
        }
    }

    private async Task ProcessAsync(JobQueue queue, ProcessingJob job, CancellationToken stoppingToken)
    {
        using var activity = WorkerTelemetry.Source.StartActivity("nomorepaper.processing.document");
        activity?.SetTag("nomorepaper.processing.attempt", job.Attempt);
        var started = time.GetTimestamp();
        var id = job.DocumentId;
        logger.DocumentStarted(id, job.Attempt);

        if (job.Attempt > RetryPolicy.MaxAttempts)
        {
            // Only possible when the Worker stopped during every attempt (crash, restart, out of memory).
            activity?.SetStatus(ActivityStatusCode.Error);
            await RecordFailureAsync(queue, job, "Processing was interrupted in every attempt", started, stoppingToken).ConfigureAwait(false);
            return;
        }

        var work = Directory.CreateTempSubdirectory("nomorepaper-");
        try
        {
            var result = await RunPipelineAsync(queue, id, work.FullName, stoppingToken).ConfigureAwait(false);
            var elapsed = time.GetElapsedTime(started);
            ProcessingLog.Completed.Add(1);
            ProcessingLog.Duration.Record(elapsed.TotalSeconds, new KeyValuePair<string, object?>(ProcessingLog.OutcomeTag, "done"));
            logger.DocumentDone(id, (long)elapsed.TotalMilliseconds, result.PageCount, result.Text.Length);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.DocumentInterrupted(id);
            throw;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            await RecordFailureAsync(queue, job, TechnicalReason(exception), started, stoppingToken).ConfigureAwait(false);
        }
        finally
        {
            DeleteWorkingCopy(id, work);
        }
    }

    private async Task<OcrResult> RunPipelineAsync(JobQueue queue, Guid id, string work, CancellationToken stoppingToken)
    {
        var document = await queue.StartAsync(id, stoppingToken).ConfigureAwait(false);
        var original = document.FindFile(DocumentFileKind.Original)
            ?? throw new InvalidOperationException("The document has no original file.");

        // The working copy has a fixed name: the received name never reaches a tool's command line.
        var input = Path.Combine(work, "original");
        var source = storage.OpenRead(id, DocumentFileKind.Original);
        await using (source.ConfigureAwait(false))
        {
            var copy = File.Create(input);
            await using (copy.ConfigureAwait(false))
            {
                await source.CopyToAsync(copy, stoppingToken).ConfigureAwait(false);
            }
        }

        var output = Path.Combine(work, "archive.pdf");
        var result = await ocr.CreateArchiveAsync(input, original.MediaType, output, stoppingToken).ConfigureAwait(false);

        StoredFile archive;
        var pdf = File.OpenRead(output);
        await using (pdf.ConfigureAwait(false))
        {
            archive = await storage.SaveArchiveAsync(id, DocumentFileKind.Archive, pdf, stoppingToken).ConfigureAwait(false);
        }

        // Moving the original and completing are both safe to repeat if the attempt fails in between.
        await storage.ArchiveOriginalAsync(id, stoppingToken).ConfigureAwait(false);
        await queue.CompleteAsync(id, result.Text, archive, stoppingToken).ConfigureAwait(false);
        return result;
    }

    private async Task RecordFailureAsync(JobQueue queue, ProcessingJob job, string reason, long started, CancellationToken stoppingToken)
    {
        var id = job.DocumentId;
        try
        {
            var next = await queue.FailAsync(id, reason, time.GetUtcNow(), stoppingToken).ConfigureAwait(false);
            var outcome = next is null ? "failed" : "retry";
            ProcessingLog.Failed.Add(1, new KeyValuePair<string, object?>(ProcessingLog.OutcomeTag, outcome));
            ProcessingLog.Duration.Record(
                time.GetElapsedTime(started).TotalSeconds, new KeyValuePair<string, object?>(ProcessingLog.OutcomeTag, outcome));
            if (next is { } nextAttemptAt)
            {
                logger.AttemptFailed(job.Attempt, id, reason, nextAttemptAt);
            }
            else
            {
                logger.DocumentFailed(id, job.Attempt, reason);
            }
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.FailureNotRecorded(id, exception.GetType().Name);
        }
    }

    // Exception messages may contain paths or tool output, so only the type (and tool and exit code) is kept.
    private static string TechnicalReason(Exception exception) => exception switch
    {
        ExternalToolException tool => $"{nameof(ExternalToolException)}: {tool.Tool} exited with code {tool.ExitCode}",
        _ => exception.GetType().Name,
    };

    private void DeleteWorkingCopy(Guid id, DirectoryInfo work)
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
