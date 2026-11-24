using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Trash;

/// <summary>
/// The owner takes an entry out of the trash (ADR-0025). A whole document is visible again as before; one
/// that was not processed yet gets its OCR job back, one waiting for recognition its recognition job. A
/// file goes back to its place (<see cref="RestoreTrashFile"/>).
/// </summary>
public sealed class RestoreTrashItem(
    ITrashStore trash,
    RestoreTrashFile files,
    IJobQueue queue,
    ICurrentAccount current,
    ILogger<RestoreTrashItem> logger)
{
    private const string Action = "restore";

    /// <returns>
    /// <see cref="ChangeOutcome.Changed"/>; <see cref="ChangeOutcome.NotFound"/> if there is no such entry of a
    /// document the account owns; <see cref="ChangeOutcome.Conflict"/> if a file cannot come back now (its
    /// document lies in the trash, or another job of the document runs); <see cref="ChangeOutcome.AccountGone"/>.
    /// </returns>
    public async Task<ChangeOutcome> RestoreAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await current.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            logger.AccountGone(current.Id, Action);
            return ChangeOutcome.AccountGone;
        }

        var item = await trash.FindLockedAsync(id, current.Id, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            logger.TrashChangeRefused(current.Id, ChangeOutcome.NotFound, Action, id, "no-entry");
            return ChangeOutcome.NotFound;
        }

        if (item.Kind != TrashItemKind.Document)
        {
            return await files.RestoreAsync(item, cancellationToken).ConfigureAwait(false);
        }

        var document = await trash.FindTrashedDocumentAsync(item.DocumentId, current.Id, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            // The entry names a document that is not in the trash: nothing to bring back.
            logger.TrashChangeRefused(current.Id, ChangeOutcome.Conflict, Action, id, "document-not-in-trash");
            return ChangeOutcome.Conflict;
        }

        document.RestoreFromTrash(current.Id);

        // Writes the document first, so the queue's statements below see it out of the trash.
        await trash.RemoveAsync(item, cancellationToken).ConfigureAwait(false);
        var requeued = await RequeueAsync(document, cancellationToken).ConfigureAwait(false);
        await trash.SaveAsync(cancellationToken).ConfigureAwait(false);
        logger.EntryRestored(item.Id, item.Kind, document.Id, current.Id);
        if (requeued is { } stage)
        {
            logger.WorkRequeued(document.Id, stage);
        }

        return ChangeOutcome.Changed;
    }

    // Moving it to the trash dropped its job; a document that still needed one gets it back. A planned
    // edit or a split suggestion in the making is not repeated: the owner asks for it again.
    private async Task<JobStage?> RequeueAsync(Document document, CancellationToken cancellationToken)
    {
        if (document.State is ProcessingState.Queued or ProcessingState.Processing)
        {
            return await queue.RequeueOcrAsync(document.Id, cancellationToken).ConfigureAwait(false) ? JobStage.Ocr : null;
        }

        return document.State == ProcessingState.Done && document.Recognition == RecognitionState.None
            && await queue.RequeueRecognitionAsync(document.Id, cancellationToken).ConfigureAwait(false)
            ? JobStage.Recognition
            : null;
    }
}
