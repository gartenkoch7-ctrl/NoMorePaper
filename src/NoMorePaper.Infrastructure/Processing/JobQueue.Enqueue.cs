using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Processing;

namespace NoMorePaper.Infrastructure.Processing;

// Queuing an edit and dropping a job (M4b). Each statement joins the caller's open transaction, which
// has locked the document row first: the order recognition and deleting use too.
public sealed partial class JobQueue
{
    /// <inheritdoc />
    public async Task<bool> EnqueueEditAsync(Guid documentId, bool recognizeAfter, CancellationToken cancellationToken)
    {
        var edit = nameof(JobStage.Edit);
        var recognition = nameof(JobStage.Recognition);
        var now = time.GetUtcNow();

        // A waiting recognition is taken over: its result would describe the old pages anyway, and it
        // follows the edit instead. One a Worker holds is left alone (the edit is refused); an expired
        // claim counts as free, its Worker loses it.
        var tookOver = await db.Database.ExecuteSqlAsync(
            $"""
            UPDATE processing_jobs SET stage = {edit}, priority = 0, attempt = 0, next_attempt_at = {now},
                claim_id = NULL, locked_until = NULL, last_error = NULL
            WHERE document_id = {documentId} AND stage = {recognition} AND (locked_until IS NULL OR locked_until < {now})
            """,
            cancellationToken).ConfigureAwait(false) > 0;
        if (tookOver)
        {
            _logger.RecognitionTakenOver(documentId);
        }
        else
        {
            var queued = await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO processing_jobs (document_id, stage, priority, attempt, next_attempt_at)
                VALUES ({documentId}, {edit}, 0, 0, {now})
                ON CONFLICT (document_id) DO NOTHING
                """,
                cancellationToken).ConfigureAwait(false);
            if (queued == 0)
            {
                _logger.EditRefusedJobPending(documentId);
                return false;
            }
        }

        var recognize = recognizeAfter || tookOver;
        await db.Database.ExecuteSqlAsync(
            $"UPDATE document_edits SET recognize_after = {recognize} WHERE document_id = {documentId}",
            cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> CancelAsync(Guid documentId, CancellationToken cancellationToken) =>
        await db.ProcessingJobs.Where(job => job.DocumentId == documentId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false) > 0;
}
