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
/// transaction, so no Worker processes a document in the trash. An edit whose new version is stored
/// already is never dropped: its files are still being moved (<see cref="EditFinisher"/>).
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
    /// (or it is in the trash already); <see cref="ChangeOutcome.Forbidden"/> unless the account owns it;
    /// <see cref="ChangeOutcome.Conflict"/> while a stored edit still moves its files.
    /// </returns>
    public async Task<ChangeOutcome> MoveAsync(Guid id, CancellationToken cancellationToken)
    {
        var (document, outcome) = await DocumentChangeGuard.LoadAsync(
            documents, current, logger, id, Action, found => found.OwnerId == current.Id, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return outcome;
        }

        return await MoveLoadedAsync(document, Action, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Moves a document the caller loaded and checked (owner) through <see cref="IDocumentFieldsStore.FindVisibleAsync"/>.</summary>
    /// <param name="action">The caller's word for the log of a refusal, e.g. <c>delete-duplicate</c>.</param>
    /// <returns>
    /// <see cref="ChangeOutcome.Changed"/>, or <see cref="ChangeOutcome.Conflict"/> (nothing changed) while a stored edit
    /// still moves its files.
    /// </returns>
    internal async Task<ChangeOutcome> MoveLoadedAsync(Document document, string action, CancellationToken cancellationToken)
    {
        // Read under the document's lock, which the Worker takes to store a version too. Dropping a stored
        // edit would leave the database naming files that never reach their place.
        if (await edits.GetAsync(document.Id, cancellationToken).ConfigureAwait(false) is { } edit
            && EditDataJson.Parse(edit.Data, edit.Kind) is { IsCommitted: true })
        {
            logger.DocumentChangeRefused(current.Id, ChangeOutcome.Conflict, action, document.Id);
            return ChangeOutcome.Conflict;
        }

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

        return ChangeOutcome.Changed;
    }
}
