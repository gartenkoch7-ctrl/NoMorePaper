using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Processing;

// The page backlog (M4b, F1): documents from before page information existed get it once, as a job at
// the edit stage without planned edit, at a low priority. Such a job never makes a version, so an edit
// asked for meanwhile replaces it.
public sealed partial class JobQueue
{
    /// <inheritdoc />
    public async Task<bool> CompleteAnalysisAsync(JobClaim claim, IReadOnlyList<PageInfo> pages, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(pages);
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            await LockDocumentAsync(claim.DocumentId, cancellationToken).ConfigureAwait(false);
            var job = await LockClaimedJobAsync(claim, cancellationToken).ConfigureAwait(false);
            if (job is null)
            {
                return false;
            }

            await ReplacePagesAsync(claim.DocumentId, pages, cancellationToken).ConfigureAwait(false);
            db.ProcessingJobs.Remove(job);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DropPendingAnalysisAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var edit = nameof(JobStage.Edit);
        var now = time.GetUtcNow();

        // Only a job nobody holds; an expired claim counts as free, its Worker loses it.
        return await db.Database.ExecuteSqlAsync(
            $"""
            DELETE FROM processing_jobs
            WHERE document_id = {documentId} AND stage = {edit} AND (locked_until IS NULL OR locked_until < {now})
              AND NOT EXISTS (SELECT 1 FROM document_edits e WHERE e.document_id = {documentId})
            """,
            cancellationToken).ConfigureAwait(false) > 0;
    }

    /// <inheritdoc />
    public Task<int> EnqueuePageBacklogAsync(int priority, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // One statement; a job that appears meanwhile (an upload, an edit, another Worker's backlog) is kept.
        var stage = nameof(JobStage.Edit);
        var done = nameof(ProcessingState.Done);
        var dueAt = now.ToUniversalTime();
        // {0} stage, {1} priority, {2} due, {3} Done.
        return db.Database.ExecuteSqlAsync(
            FormattableStringFactory.Create(
                $$"""
                INSERT INTO processing_jobs (document_id, stage, priority, attempt, next_attempt_at)
                SELECT d.id, {0}, {1}, 0, {2} FROM documents d
                WHERE d.state = {3} AND {{TrashSql.NotInTrash}}
                  AND NOT EXISTS (SELECT 1 FROM document_pages p WHERE p.document_id = d.id)
                  AND NOT EXISTS (SELECT 1 FROM processing_jobs j WHERE j.document_id = d.id)
                ON CONFLICT (document_id) DO NOTHING
                """,
                stage,
                priority,
                dueAt,
                done),
            cancellationToken);
    }
}
