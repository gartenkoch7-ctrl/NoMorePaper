using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Domain.Processing;

namespace NoMorePaper.Infrastructure.Processing;

// Failed and interrupted attempts of every stage; claiming and OCR are in JobQueue.cs.
public sealed partial class JobQueue
{
    /// <inheritdoc />
    public async Task<RecordedFailure> FailAsync(
        JobClaim claim, string technicalError, bool permanent, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentException.ThrowIfNullOrWhiteSpace(technicalError);

        // Changes of the failed attempt (e.g. a half-done completion) must not be saved with the failure.
        db.ChangeTracker.Clear();
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var job = await LockClaimedJobAsync(claim, cancellationToken).ConfigureAwait(false);
            if (job is null)
            {
                return new RecordedFailure(FailureOutcome.ClaimLost);
            }

            var error = technicalError.Length > Document.MaxFailureReasonLength
                ? technicalError[..Document.MaxFailureReasonLength]
                : technicalError;
            var next = permanent ? null : RetryPolicy.NextAttemptAt(Math.Max(job.Attempt, 1), now.ToUniversalTime());
            if (next is { } retryAt)
            {
                job.LastError = error;
                job.NextAttemptAt = retryAt;
                job.ClaimId = null;
                job.LockedUntil = null;
            }
            else
            {
                await GiveUpAsync(job, error, cancellationToken).ConfigureAwait(false);
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return next is null ? new RecordedFailure(FailureOutcome.GivenUp) : new RecordedFailure(FailureOutcome.RetryScheduled, next);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ReleaseAsync(JobClaim claim, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);

        // Taking the attempt back happens at most once per claim: the claim id goes with it.
        var released = await db.ProcessingJobs
            .Where(job => job.DocumentId == claim.DocumentId && job.ClaimId == claim.ClaimId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(job => job.Attempt, job => job.Attempt - 1)
                    .SetProperty(job => job.ClaimId, (Guid?)null)
                    .SetProperty(job => job.LockedUntil, (DateTimeOffset?)null),
                cancellationToken)
            .ConfigureAwait(false);
        return released > 0;
    }

    private async Task GiveUpAsync(ProcessingJob job, string error, CancellationToken cancellationToken)
    {
        db.ProcessingJobs.Remove(job);

        // Recognition is an extra: without it the document is still done, only without fields.
        if (job.Stage == JobStage.Recognition)
        {
            return;
        }

        var document = await FindDocumentAsync(job.DocumentId, cancellationToken).ConfigureAwait(false);

        // Queued: the attempt failed before it could even mark the document as processing.
        if (document.State == ProcessingState.Queued)
        {
            document.StartProcessing();
        }

        if (document.State == ProcessingState.Processing)
        {
            document.Fail(error);
        }
    }
}
