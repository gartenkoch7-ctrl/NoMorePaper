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
    public async Task<RecognitionCommit> CompleteRecognitionAsync(JobClaim claim, DocumentFields? fields, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            // The document first, then the job: deleting a document locks them in this order too (the
            // job goes by cascade), so the two never wait for each other. A confirmation committed
            // before is seen below; confirmed fields are never overwritten.
            await db.Database.ExecuteSqlAsync($"SELECT 1 FROM documents WHERE id = {claim.DocumentId} FOR UPDATE", cancellationToken)
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
