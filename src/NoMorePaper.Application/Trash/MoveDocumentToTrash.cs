using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Trash;

/// <summary>
/// The owner moves a document to the trash (ADR-0025): it is hidden everywhere for 30 days and can be
/// restored until then; its files stay where they are. Its job and planned edit are dropped in the same
/// transaction, so no Worker processes a document in the trash.
/// </summary>
public sealed class MoveDocumentToTrash(
    IDocumentFieldsStore documents,
    ITrashStore trash,
    IJobQueue queue,
    IDocumentEdits edits,
    ICurrentAccount current,
    TimeProvider time,
    ILogger<MoveDocumentToTrash> logger)
{
    private const string Action = "move-to-trash";

    /// <returns>
    /// <see cref="ChangeOutcome.Changed"/>; <see cref="ChangeOutcome.NotFound"/> if the account may not see it
    /// (or it is in the trash already); <see cref="ChangeOutcome.Forbidden"/> unless the account owns it.
    /// </returns>
    public async Task<ChangeOutcome> MoveAsync(Guid id, CancellationToken cancellationToken)
    {
        var (document, outcome) = await DocumentChangeGuard.LoadAsync(
            documents, current, logger, id, Action, found => found.OwnerId == current.Id, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return outcome;
        }

        await MoveLoadedAsync(document, cancellationToken).ConfigureAwait(false);
        return ChangeOutcome.Changed;
    }

    /// <summary>Moves a document the caller loaded and checked (owner) through <see cref="IDocumentFieldsStore.FindVisibleAsync"/>.</summary>
    internal async Task MoveLoadedAsync(Document document, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        document.MoveToTrash(current.Id, now);
        var item = TrashItem.ForDocument(document, now);
        await trash.AddAsync(item, cancellationToken).ConfigureAwait(false);

        // In the change the store opened: the document row is locked first, then the job (the order
        // recognition uses), and a Worker running the job loses its claim.
        var hadJob = await queue.CancelAsync(document.Id, cancellationToken).ConfigureAwait(false);
        await edits.DeleteAsync(document.Id, cancellationToken).ConfigureAwait(false);
        await documents.SaveAsync(cancellationToken).ConfigureAwait(false);
        logger.DocumentTrashed(document.Id, current.Id, item.Id);
        if (hadJob)
        {
            logger.PendingWorkDropped(document.Id);
        }
    }
}
