using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Editing;
using NoMorePaper.Infrastructure.Trash;

namespace NoMorePaper.Infrastructure.Processing;

// The edit stage (M4b): storing a carried-out edit, ending it, and giving it up. Documents are locked
// in the order of their ids, then the job, like every other change of a document (see LockDocumentAsync).
public sealed partial class JobQueue
{
    /// <inheritdoc />
    public Task<Document?> ReadDocumentAsync(Guid documentId, CancellationToken cancellationToken) =>
        db.Documents.AsNoTracking().SingleOrDefaultAsync(document => document.Id == documentId, cancellationToken);

    /// <inheritdoc />
    public async Task<EditCommitResult> CompleteEditAsync(JobClaim claim, EditCompletion completion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(completion);
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            Guid[] ids = completion.OtherId is { } other ? [claim.DocumentId, other] : [claim.DocumentId];
            foreach (var id in ids.Order())
            {
                await LockDocumentAsync(id, cancellationToken).ConfigureAwait(false);
            }

            var job = await LockClaimedJobAsync(claim, cancellationToken).ConfigureAwait(false);
            if (job is null)
            {
                return EditCommitResult.ClaimLost;
            }

            var document = await FindDocumentAsync(claim.DocumentId, cancellationToken).ConfigureAwait(false);
            var otherDocument = completion.OtherId is { } otherId
                ? await db.Documents.SingleOrDefaultAsync(found => found.Id == otherId, cancellationToken).ConfigureAwait(false)
                : null;
            if (!completion.Apply(document, otherDocument))
            {
                // Nothing tracked may reach a later save of this context (e.g. the failure it records next).
                db.ChangeTracker.Clear();
                return EditCommitResult.Changed;
            }

            if (completion.Text is { } text)
            {
                document.ReplaceContent(text);
            }

            if (completion.Pages is { } pages)
            {
                await ReplacePagesAsync(claim.DocumentId, pages, cancellationToken).ConfigureAwait(false);
            }

            db.AddRange(completion.TrashItems.Select(TrashItemRow.From));

            // A document the edit moved to the trash (the appended one) keeps no work, as MoveDocumentToTrash does.
            if (otherDocument is { IsInTrash: true })
            {
                await db.ProcessingJobs.Where(found => found.DocumentId == otherDocument.Id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
                await Edits(otherDocument.Id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            }

            if (completion.Final)
            {
                await EndEditAsync(job, document, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await db.Database.ExecuteSqlAsync(
                    $"UPDATE document_edits SET data = CAST({completion.Data} AS jsonb) WHERE document_id = {claim.DocumentId}",
                    cancellationToken).ConfigureAwait(false);
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return EditCommitResult.Committed;
        }
    }

    /// <inheritdoc />
    public async Task<bool> FinishEditAsync(JobClaim claim, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            await LockDocumentAsync(claim.DocumentId, cancellationToken).ConfigureAwait(false);
            var job = await LockClaimedJobAsync(claim, cancellationToken).ConfigureAwait(false);
            if (job is null)
            {
                return false;
            }

            var document = await ReadDocumentAsync(claim.DocumentId, cancellationToken).ConfigureAwait(false);
            await EndEditAsync(job, document, cancellationToken).ConfigureAwait(false);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
    }

    // A given-up edit leaves the document as it was: its planned edit goes, and a recognition it had taken
    // over runs again. The caller holds the locks of document and job.
    private async Task GiveUpEditAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        var document = await ReadDocumentAsync(job.DocumentId, cancellationToken).ConfigureAwait(false);
        await EndEditAsync(job, document, cancellationToken).ConfigureAwait(false);
    }

    // Removes the planned edit; the job moves on to recognition when the edit asked for it and there is
    // something to recognize (as after OCR), else it goes. A page analysis has no planned edit.
    private async Task EndEditAsync(ProcessingJob job, Document? document, CancellationToken cancellationToken)
    {
        var edit = await Edits(job.DocumentId).AsNoTracking().SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        await Edits(job.DocumentId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        var recognize = edit is { RecognizeAfter: true }
            && document is { IsInTrash: false, OwnerId: not null }
            && !string.IsNullOrWhiteSpace(document.ContentText)
            && document.Recognition != RecognitionState.Confirmed;
        if (!recognize)
        {
            db.ProcessingJobs.Remove(job);
            return;
        }

        job.Stage = JobStage.Recognition;
        job.Attempt = 0;
        job.NextAttemptAt = time.GetUtcNow();
        job.ClaimId = null;
        job.LockedUntil = null;
        job.LastError = null;
    }

    private IQueryable<DocumentEditRow> Edits(Guid documentId) =>
        db.Set<DocumentEditRow>().Where(row => row.DocumentId == documentId);
}
