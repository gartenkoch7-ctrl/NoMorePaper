using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Editing;

/// <summary>
/// The second half of a stored edit (ADR-0025): moves the files the commit recorded, then ends the edit.
/// Every step is safe to repeat, so a Worker interrupted anywhere after the commit finishes it on the next
/// claim. The staged edited version is moved last of its steps: while it waits, the previous edited
/// version has not been replaced yet.
/// </summary>
internal sealed class EditFinisher(IDocumentStorage storage, IEditPageStorage pageImages, ILogger<EditStageRunner> logger)
{
    /// <returns><c>false</c> when the claim was lost before the edit could end; the files are in place anyway.</returns>
    public async Task<bool> FinishAsync(IJobQueue queue, JobClaim claim, EditData data, CancellationToken cancellationToken)
    {
        var id = claim.DocumentId;
        var moves = data.Finish ?? [];
        if (Find(moves, FinishMoveKind.PreviousEdited) is { } previous && storage.IsStaged(id, data.EditId, DocumentFileKind.Edited))
        {
            await storage.MoveToTrashAsync(id, DocumentFileKind.Edited, previous.TrashItemId, cancellationToken).ConfigureAwait(false);
        }

        if (Find(moves, FinishMoveKind.NewEdited) is { } created)
        {
            await storage.TrashStagedAsync(id, data.EditId, created.TrashItemId, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await storage.PublishStagedAsync(id, data.EditId, DocumentFileKind.Edited, cancellationToken).ConfigureAwait(false);
        }

        await storage.PublishStagedAsync(id, data.EditId, DocumentFileKind.Archive, cancellationToken).ConfigureAwait(false);
        if (Find(moves, FinishMoveKind.Original) is { } original)
        {
            await storage.MoveToTrashAsync(id, DocumentFileKind.Original, original.TrashItemId, cancellationToken).ConfigureAwait(false);
        }

        Discard(id, data.EditId);
        if (!await queue.FinishEditAsync(claim, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        logger.EditFinished(id);
        return true;
    }

    /// <summary>Removes the page images and leftovers of an edit; a failure only costs space.</summary>
    public void Discard(Guid id, Guid editId)
    {
        try
        {
            pageImages.Discard(id, editId);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.EditFilesNotDiscarded(id, exception.GetType().Name);
        }
    }

    private static FinishMove? Find(IReadOnlyList<FinishMove> moves, FinishMoveKind kind) => moves.FirstOrDefault(move => move.Kind == kind);
}
