using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Processing;

namespace NoMorePaper.Application.Editing;

/// <summary>
/// The edit stage (<see cref="JobStage.Edit"/>, ADR-0025): carries out the planned edit of the job's
/// document (<see cref="VersionStep"/>, <see cref="SplitStep"/>), finishes one whose commit an interruption
/// cut short (<see cref="EditFinisher"/>), or, without planned edit, reads the pages of an old document
/// (<see cref="PageAnalysis"/>). Each attempt works in a folder of its own. A failure is recorded with a
/// technical reason only and retried by the <see cref="RetryPolicy"/>; once given up, the document keeps
/// its previous version and the edit's files are removed.
/// </summary>
internal sealed class EditStageRunner(
    IDocumentEdits edits,
    IWorkingFolder workingFolder,
    VersionStep versions,
    SplitStep splits,
    PageAnalysis analysis,
    EditFinisher finisher,
    TimeProvider time,
    ILogger<EditStageRunner> logger) : IStageRunner
{
    public async Task RunAsync(IJobQueue queue, JobClaim claim, CancellationToken stoppingToken)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(claim);
        var id = claim.DocumentId;
        var edit = await edits.GetAsync(id, stoppingToken).ConfigureAwait(false);
        var data = edit is null ? null : EditDataJson.Parse(edit.Data, edit.Kind);
        if (edit is not null && data is null)
        {
            await RecordFailureAsync(queue, claim, null, "The planned edit cannot be read", permanent: true, stoppingToken).ConfigureAwait(false);
            return;
        }

        // Finishing a stored edit only moves files; it must not be given up for the attempts before it.
        if (data is not { IsCommitted: true } && claim.Attempt > RetryPolicy.MaxAttempts)
        {
            await RecordFailureAsync(queue, claim, data, "Processing was interrupted in every attempt", permanent: false, stoppingToken)
                .ConfigureAwait(false);
            return;
        }

        if (edit is not null)
        {
            logger.EditStarted(id, edit.Kind, claim.Attempt);
        }

        var work = workingFolder.CreateAttemptFolder();
        try
        {
            var done = edit is null || data is null
                ? await analysis.RunAsync(queue, claim, work, stoppingToken).ConfigureAwait(false)
                : await RunEditAsync(queue, claim, edit, data, work, stoppingToken).ConfigureAwait(false);
            if (!done)
            {
                logger.ClaimLost(claim.Attempt, id);
            }
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            // The queue never gives up an edit stored meanwhile (even in this attempt): only its files are left to move.
            var (reason, permanent) = Describe(exception);
            await RecordFailureAsync(queue, claim, data, reason, permanent, stoppingToken).ConfigureAwait(false);
        }
        finally
        {
            DeleteWorkingFolder(id, work);
        }
    }

    private async Task<bool> RunEditAsync(
        IJobQueue queue, JobClaim claim, DocumentEdit edit, EditData data, string work, CancellationToken stoppingToken)
    {
        if (data.IsCommitted)
        {
            return await finisher.FinishAsync(queue, claim, data, stoppingToken).ConfigureAwait(false);
        }

        var document = await queue.ReadAsync(claim, stoppingToken).ConfigureAwait(false);
        if (document is null)
        {
            return false;
        }

        return edit.Kind == EditKind.Split
            ? await splits.RunAsync(queue, claim, edit, data, document, work, stoppingToken).ConfigureAwait(false)
            : await versions.RunAsync(queue, claim, edit, data, document, work, stoppingToken).ConfigureAwait(false);
    }

    private async Task RecordFailureAsync(
        IJobQueue queue, JobClaim claim, EditData? data, string reason, bool permanent, CancellationToken stoppingToken)
    {
        var id = claim.DocumentId;
        try
        {
            var recorded = await queue.FailAsync(claim, reason, permanent, time.GetUtcNow(), stoppingToken).ConfigureAwait(false);
            switch (recorded.Outcome)
            {
                case FailureOutcome.ClaimLost:
                    logger.ClaimLost(claim.Attempt, id);
                    break;
                case FailureOutcome.RetryScheduled:
                    logger.EditAttemptFailed(claim.Attempt, id, reason, recorded.NextAttemptAt!.Value);
                    break;
                default:
                    logger.EditGivenUp(id, claim.Attempt, reason);

                    // A stored edit keeps its staged files: the database refers to them.
                    if (data is { IsCommitted: false })
                    {
                        finisher.Discard(id, data.EditId);
                    }

                    break;
            }
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.FailureNotRecorded(id, exception.GetType().Name);
        }
    }

    // Exception messages may contain paths or tool output, so only fixed reasons, types, tools and exit codes are kept.
    private static (string Reason, bool Permanent) Describe(Exception exception) => exception switch
    {
        EditFailedException failed => (failed.Message, failed.IsPermanent),
        ExternalToolException tool => ($"{nameof(ExternalToolException)}: {tool.Tool} exited with code {tool.ExitCode}", tool.IsPermanent),

        // A page image that is no readable JPEG, or a source OCR cannot take, fails every attempt alike.
        InvalidDataException or NotSupportedException => (exception.GetType().Name, true),
        _ => (exception.GetType().Name, false),
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
