using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Trash;

namespace NoMorePaper.Application.Classification;

/// <summary>
/// The owner deletes a possible duplicate from "to review": it goes to the trash
/// (<see cref="MoveDocumentToTrash"/>) and can be restored for 30 days. Only possible duplicates are
/// deleted here; any other document goes to the trash from the viewer.
/// </summary>
public sealed class DeleteDuplicate(
    IDocumentFieldsStore documents, MoveDocumentToTrash trash, ICurrentAccount current, ILogger<DeleteDuplicate> logger)
{
    private const string Action = "delete-duplicate";

    /// <returns>
    /// <see cref="ChangeOutcome.Conflict"/> if the document is no possible duplicate or a stored edit still moves its files.
    /// </returns>
    public async Task<ChangeOutcome> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var (document, outcome) = await DocumentChangeGuard.LoadAsync(
            documents, current, logger, id, Action, found => found.OwnerId == current.Id, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return outcome;
        }

        if (document.PossibleDuplicateOf is null)
        {
            logger.DocumentChangeRefused(current.Id, ChangeOutcome.Conflict, Action, id);
            return ChangeOutcome.Conflict;
        }

        var moved = await trash.MoveLoadedAsync(document, Action, cancellationToken).ConfigureAwait(false);
        if (moved == ChangeOutcome.Changed)
        {
            logger.DuplicateDeleted(id, current.Id);
        }

        return moved;
    }
}
