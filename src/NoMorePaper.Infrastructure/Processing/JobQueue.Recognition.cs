using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Recognition;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Infrastructure.Processing;

// The recognition stage of the queue and the backlog; claiming, failing and handing back are shared
// with OCR in JobQueue.cs.
public sealed partial class JobQueue
{
    /// <inheritdoc />
    public async Task<Document?> ReadAsync(JobClaim claim, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        return await db.Documents.AsNoTracking()
            .Where(document => document.Id == claim.DocumentId
                && db.ProcessingJobs.Any(job => job.DocumentId == claim.DocumentId && job.ClaimId == claim.ClaimId))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<RecognitionCommit> CompleteRecognitionAsync(
        JobClaim claim, DocumentFields? fields, bool wasPrivate, Guid? wasOwner, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            // The document first, then the job: deleting a document locks them in this order too (the
            // job goes by cascade), so the two never wait for each other. A confirmation committed
            // before is seen below; confirmed fields are never overwritten.
            await db.Database.ExecuteSqlAsync($"SELECT 1 FROM documents WHERE id = {claim.DocumentId} FOR NO KEY UPDATE", cancellationToken)
                .ConfigureAwait(false);
            var job = await LockClaimedJobAsync(claim, cancellationToken).ConfigureAwait(false);
            if (job is null)
            {
                return RecognitionCommit.ClaimLost;
            }

            var commit = RecognitionCommit.JobRemoved;
            if (fields is not null)
            {
                var document = await FindDocumentAsync(claim.DocumentId, cancellationToken).ConfigureAwait(false);

                // Shared since the fields were made from the owner's private view (ADR-0023, addendum),
                // or handed over to another account since they were made from the previous owner's lists
                // (ADR-0024): checked under the row lock, so neither can slip in before the commit.
                // Nothing is taken; the job is handed back like ReleaseAsync does.
                var handBack = document.OwnerId != wasOwner ? RecognitionCommit.OwnerChanged
                    : wasPrivate && !document.IsPrivate ? RecognitionCommit.SharedMeanwhile
                    : (RecognitionCommit?)null;
                if (handBack is { } reason)
                {
                    job.Attempt--;
                    job.ClaimId = null;
                    job.LockedUntil = null;
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    return reason;
                }

                if (document.Recognition != RecognitionState.Confirmed)
                {
                    document.Classify(fields);
                    commit = RecognitionCommit.Classified;
                }
            }

            db.ProcessingJobs.Remove(job);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return commit;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RequeueRecognitionAsync(Guid documentId, CancellationToken cancellationToken)
    {
        // One statement in the caller's transaction (ExecuteSql joins it): a new unclaimed job, or
        // the recognition job reset: a running attempt loses its claim and its result is discarded. An
        // OCR job is left alone; recognition follows it with the owner of that moment.
        var stage = nameof(JobStage.Recognition);
        var done = nameof(ProcessingState.Done);
        var now = time.GetUtcNow();
        var changed = await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO processing_jobs (document_id, stage, priority, attempt, next_attempt_at)
            SELECT d.id, {stage}, 0, 0, {now} FROM documents d
            WHERE d.id = {documentId} AND d.state = {done} AND d.owner_id IS NOT NULL AND d.content_text ~ '\S'
            ON CONFLICT (document_id) DO UPDATE SET
                priority = 0, attempt = 0, next_attempt_at = EXCLUDED.next_attempt_at,
                claim_id = NULL, locked_until = NULL, last_error = NULL
            WHERE processing_jobs.stage = {stage}
            """,
            cancellationToken).ConfigureAwait(false);
        return changed > 0;
    }

    /// <inheritdoc />
    public Task<int> EnqueueRecognitionBacklogAsync(int priority, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // One statement; a job that appears meanwhile (a new upload, another Worker's backlog) is kept.
        var stage = nameof(JobStage.Recognition);
        var done = nameof(ProcessingState.Done);
        var none = nameof(RecognitionState.None);
        var dueAt = now.ToUniversalTime();
        return db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO processing_jobs (document_id, stage, priority, attempt, next_attempt_at)
            SELECT d.id, {stage}, {priority}, 0, {dueAt} FROM documents d
            WHERE d.state = {done} AND d.recognition = {none} AND d.owner_id IS NOT NULL AND d.content_text ~ '\S'
              AND NOT EXISTS (SELECT 1 FROM processing_jobs j WHERE j.document_id = d.id)
            ON CONFLICT (document_id) DO NOTHING
            """,
            cancellationToken);
    }
}
