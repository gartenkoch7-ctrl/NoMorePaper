using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Documents;

namespace NoMorePaper.Application.Classification;

/// <summary>
/// The owner deletes a possible duplicate for good. Until the recycle bin arrives (M4), only possible
/// duplicates can be deleted; the record goes first, then the files, so no record ever points to
/// missing files.
/// </summary>
public sealed class DeleteDuplicate(
    IDocumentFieldsStore documents, IDocumentStorage storage, ICurrentAccount current, ILogger<DeleteDuplicate> logger)
{
    private const string Action = "delete-duplicate";

    /// <returns><see cref="ChangeOutcome.Conflict"/> if the document is no possible duplicate.</returns>
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

        // Once the record is gone the files must go too, even if the request is dropped now.
        await documents.DeleteAsync(document, CancellationToken.None).ConfigureAwait(false);
        logger.DuplicateDeleted(id, current.Id);
        try
        {
            storage.Delete(id);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The document is deleted; leftover files only cost space.
            logger.DeletedFilesLeft(id, exception.GetType().Name);
        }

        return ChangeOutcome.Changed;
    }
}
