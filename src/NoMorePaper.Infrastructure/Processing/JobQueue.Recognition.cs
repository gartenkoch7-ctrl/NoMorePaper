using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Recognition;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Persistence;

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
            // The document first, then the job (see LockDocumentAsync). A confirmation committed before
            // is seen below; confirmed fields are never overwritten.
            await LockDocumentAsync(claim.DocumentId, cancellationToken).ConfigureAwait(false);
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
        // In the caller's transaction (ExecuteSql joins it): a new unclaimed job, or the recognition job
        // reset: a running attempt loses its claim and its result is discarded. A page analysis (an edit
        // job without planned edit, M4b) becomes the recognition; the backlog reads its pages on a later
        // start. An OCR or separation job is left alone, recognition follows it with the owner of that
        // moment; so it does a planned edit, which is told to recognize afterwards (EndEditAsync).
        var stage = nameof(JobStage.Recognition);
        var edit = nameof(JobStage.Edit);
        var done = nameof(ProcessingState.Done);
        var now = time.GetUtcNow();
        // {0} stage, {1} now, {2} document, {3} Done, {4} edit.
        var changed = await db.Database.ExecuteSqlAsync(
            FormattableStringFactory.Create(
                $$"""
                INSERT INTO processing_jobs (document_id, stage, priority, attempt, next_attempt_at)
                SELECT d.id, {0}, 0, 0, {1} FROM documents d
                WHERE d.id = {2} AND d.state = {3} AND d.owner_id IS NOT NULL AND d.content_text ~ '\S' AND {{TrashSql.NotInTrash}}
                ON CONFLICT (document_id) DO UPDATE SET
                    stage = {0}, priority = 0, attempt = 0, next_attempt_at = EXCLUDED.next_attempt_at,
                    claim_id = NULL, locked_until = NULL, last_error = NULL
                WHERE processing_jobs.stage = {0}
                   OR (processing_jobs.stage = {4}
                       AND NOT EXISTS (SELECT 1 FROM document_edits e WHERE e.document_id = processing_jobs.document_id))
                """,
                stage,
                now,
                documentId,
                done,
                edit),
            cancellationToken).ConfigureAwait(false);
        var afterEdit = await db.Database.ExecuteSqlAsync(
            $"UPDATE document_edits SET recognize_after = TRUE WHERE document_id = {documentId}",
            cancellationToken).ConfigureAwait(false);
        return changed > 0 || afterEdit > 0;
    }

    /// <inheritdoc />
    public Task<int> EnqueueRecognitionBacklogAsync(int priority, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // One statement; a job that appears meanwhile (a new upload, another Worker's backlog) is kept.
        var stage = nameof(JobStage.Recognition);
        var done = nameof(ProcessingState.Done);
        var none = nameof(RecognitionState.None);
        var dueAt = now.ToUniversalTime();
        // {0} stage, {1} priority, {2} due, {3} Done, {4} no fields.
        return db.Database.ExecuteSqlAsync(
            FormattableStringFactory.Create(
                $$"""
                INSERT INTO processing_jobs (document_id, stage, priority, attempt, next_attempt_at)
                SELECT d.id, {0}, {1}, 0, {2} FROM documents d
                WHERE d.state = {3} AND d.recognition = {4} AND d.owner_id IS NOT NULL AND d.content_text ~ '\S'
                  AND {{TrashSql.NotInTrash}}
                  AND NOT EXISTS (SELECT 1 FROM processing_jobs j WHERE j.document_id = d.id)
                ON CONFLICT (document_id) DO NOTHING
                """,
                stage,
                priority,
                dueAt,
                done,
                none),
            cancellationToken);
    }
}
