using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Recognition;
using NoMorePaper.Application.Telemetry;
using NoMorePaper.Domain.Editing;
using NoMorePaper.Domain.Processing;

namespace NoMorePaper.Application.Separation;

/// <summary>
/// The <see cref="JobStage.Separation"/> stage of a claimed job, between OCR and recognition of a
/// multi-page scan: the page signs (<see cref="SeparationHeuristics"/>) and one request to the local LLM
/// make a <see cref="SplitSuggestion"/>, which only waits in "to review"; nothing is split before it is
/// accepted. Recognition follows in any case.
/// </summary>
/// <remarks>
/// The suggestion is an extra: when the LLM fails, the page signs alone count, and when the stage itself
/// fails in its last attempt, recognition follows without suggestion (a job given up at this stage would
/// leave the document without recognition). A busy LLM hands the job back without counting the attempt.
/// Exceptions while the Worker stops are passed on, so the caller hands the job back.
/// </remarks>
internal sealed class SeparationStageRunner(
    IDocumentPages pages,
    ISeparationAssessor assessor,
    ISeparationQueue separation,
    TimeProvider time,
    ILogger<SeparationStageRunner> logger) : IStageRunner
{
    public async Task RunAsync(IJobQueue queue, JobClaim claim, CancellationToken stoppingToken)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(claim);
        using var activity = ApplicationTelemetry.Source.StartActivity("nomorepaper.separation.document");
        activity?.SetTag("nomorepaper.separation.attempt", claim.Attempt);
        var started = time.GetTimestamp();
        var id = claim.DocumentId;
        logger.SeparationStarted(id, claim.Attempt);

        if (claim.Attempt > RetryPolicy.MaxAttempts)
        {
            // Only possible when the Worker was killed during every attempt (crash, out of memory).
            await SkipAsync(claim, "Separation was interrupted in every attempt", stoppingToken).ConfigureAwait(false);
            return;
        }

        try
        {
            if (await queue.ReadAsync(claim, stoppingToken).ConfigureAwait(false) is null)
            {
                logger.ClaimLost(claim.Attempt, id);
                return;
            }

            var stack = await pages.ListAsync(id, stoppingToken).ConfigureAwait(false);
            var suggestion = await SuggestAsync(id, stack, stoppingToken).ConfigureAwait(false);
            if (!await separation.CompleteSeparationAsync(claim, suggestion, stoppingToken).ConfigureAwait(false))
            {
                logger.ClaimLost(claim.Attempt, id);
                return;
            }

            Report(id, stack.Count, suggestion, started);
        }
        catch (LlmBusyException) when (!stoppingToken.IsCancellationRequested)
        {
            // Nothing was asked, so the attempt does not count; the job is due again at once.
            var released = await queue.ReleaseAsync(claim, stoppingToken).ConfigureAwait(false);
            logger.LlmBusy(id, released);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            await RecordFailureAsync(queue, claim, exception.GetType().Name, stoppingToken).ConfigureAwait(false);
        }
    }

    // A digital PDF or a single page has nothing to separate; the queue sends only scanned stacks here,
    // but the pages are checked again, as they may have been replaced since.
    private async Task<SplitSuggestion?> SuggestAsync(Guid id, IReadOnlyList<PageInfo> stack, CancellationToken stoppingToken)
    {
        if (!SeparationHeuristics.IsScannedStack(stack))
        {
            return null;
        }

        var candidates = SeparationHeuristics.Find(stack);
        IReadOnlyList<int> senderChanges = [];
        try
        {
            senderChanges = (await assessor.AssessAsync(stack, stoppingToken).ConfigureAwait(false)).Boundaries;
        }
        catch (Exception exception) when (exception is not LlmBusyException && !stoppingToken.IsCancellationRequested)
        {
            logger.AssessmentFailed(id, exception.GetType().Name);
        }

        return candidates.ToSuggestion(senderChanges);
    }

    private void Report(Guid id, int pageCount, SplitSuggestion? suggestion, long started)
    {
        var elapsed = time.GetElapsedTime(started);
        var outcome = suggestion is null ? "none" : "suggested";
        SeparationLog.Completed.Add(1, new KeyValuePair<string, object?>(SeparationLog.OutcomeTag, outcome));
        SeparationLog.Duration.Record(elapsed.TotalSeconds, new KeyValuePair<string, object?>(SeparationLog.OutcomeTag, outcome));
        if (suggestion is null)
        {
            logger.NoSplitFound(id, pageCount, (long)elapsed.TotalMilliseconds);
        }
        else
        {
            logger.SplitSuggested(id, suggestion.Parts.Count, suggestion.BlankPages.Count, (long)elapsed.TotalMilliseconds);
        }
    }

    // Retried like the other stages while attempts are left; in the last one recognition follows without suggestion.
    private async Task RecordFailureAsync(IJobQueue queue, JobClaim claim, string reason, CancellationToken stoppingToken)
    {
        if (claim.Attempt >= RetryPolicy.MaxAttempts)
        {
            await SkipAsync(claim, reason, stoppingToken).ConfigureAwait(false);
            return;
        }

        var id = claim.DocumentId;
        try
        {
            // Attempts are left, so the queue schedules the next one and never gives the job up here.
            var recorded = await queue.FailAsync(claim, reason, permanent: false, time.GetUtcNow(), stoppingToken).ConfigureAwait(false);
            if (recorded.Outcome == FailureOutcome.ClaimLost)
            {
                logger.ClaimLost(claim.Attempt, id);
                return;
            }

            logger.AttemptFailed(claim.Attempt, id, reason, recorded.NextAttemptAt ?? time.GetUtcNow());
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.OutcomeNotRecorded(id, exception.GetType().Name);
        }
    }

    private async Task SkipAsync(JobClaim claim, string reason, CancellationToken stoppingToken)
    {
        var id = claim.DocumentId;
        try
        {
            if (!await separation.CompleteSeparationAsync(claim, suggestion: null, stoppingToken).ConfigureAwait(false))
            {
                logger.ClaimLost(claim.Attempt, id);
                return;
            }

            SeparationLog.Completed.Add(1, new KeyValuePair<string, object?>(SeparationLog.OutcomeTag, "skipped"));
            logger.SeparationSkipped(id, claim.Attempt, reason);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.OutcomeNotRecorded(id, exception.GetType().Name);
        }
    }
}
