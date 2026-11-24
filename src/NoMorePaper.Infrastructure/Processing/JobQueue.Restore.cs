using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Processing;

// A document back from the trash (M4b): moving it there dropped its job, so one that was not processed yet
// needs its OCR job again.
public sealed partial class JobQueue
{
    /// <inheritdoc />
    public async Task<bool> RequeueOcrAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var stage = nameof(JobStage.Ocr);
        var queued = nameof(ProcessingState.Queued);
        var processing = nameof(ProcessingState.Processing);
        var now = time.GetUtcNow();

        // One statement in the caller's transaction (ExecuteSql joins it). A job that exists already is kept.
        // {0} stage, {1} now, {2} document, {3} Queued, {4} Processing.
        var changed = await db.Database.ExecuteSqlAsync(
            FormattableStringFactory.Create(
                $$"""
                INSERT INTO processing_jobs (document_id, stage, priority, attempt, next_attempt_at)
                SELECT d.id, {0}, 0, 0, {1} FROM documents d
                WHERE d.id = {2} AND d.state IN ({3}, {4}) AND {{TrashSql.NotInTrash}}
                ON CONFLICT (document_id) DO NOTHING
                """,
                stage,
                now,
                documentId,
                queued,
                processing),
            cancellationToken).ConfigureAwait(false);
        return changed > 0;
    }
}
