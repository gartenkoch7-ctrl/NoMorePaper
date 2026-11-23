using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Documents;

namespace NoMorePaper.Application.Trash;

/// <summary>
/// Removes a trash entry for good, shared by the owner's removal, emptying and the daily purge. The rows
/// go first, then the files, so no row ever points to missing files: a file left behind only costs space.
/// A whole document goes with every trash entry of it, so the trash folders of those entries go too.
/// </summary>
public sealed class TrashEraser(ITrashStore trash, IDocumentStorage storage, ILogger<TrashEraser> logger)
{
    /// <summary>Removes the entry for good, in a change of its own that is committed here.</summary>
    /// <param name="ownerId">Only an entry of a document this account owns; <c>null</c> for any account (the daily purge).</param>
    /// <returns>The removed entry, or <c>null</c> if it does not exist (any more) or belongs to another account.</returns>
    internal async Task<TrashItem?> EraseAsync(Guid id, Guid? ownerId, CancellationToken cancellationToken)
    {
        var item = await trash.FindLockedAsync(id, ownerId, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            // Frees a lock taken on a document of another account.
            await trash.SaveAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        IReadOnlyList<Guid> folders = [item.Id];
        var documentGone = false;
        if (item.Kind == TrashItemKind.Document
            && await trash.DeleteDocumentAsync(item.DocumentId, cancellationToken).ConfigureAwait(false) is { } entries)
        {
            folders = entries;
            documentGone = true;
        }
        else
        {
            if (item.Kind == TrashItemKind.Document)
            {
                logger.StaleDocumentEntry(item.Id, item.DocumentId);
            }

            await trash.RemoveAsync(item, cancellationToken).ConfigureAwait(false);
        }

        await trash.SaveAsync(cancellationToken).ConfigureAwait(false);

        // Committed: the files must go now, even if the request is dropped.
        if (documentGone)
        {
            TryDelete(item.DocumentId, () => storage.Delete(item.DocumentId));
            logger.DocumentErased(item.DocumentId, folders.Count);
        }

        foreach (var folder in folders)
        {
            TryDelete(folder, () => storage.PurgeTrash(folder));
        }

        logger.EntryErased(item.Id, item.Kind, item.DocumentId);
        return item;
    }

    private void TryDelete(Guid id, Action delete)
    {
        try
        {
            delete();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.ErasedFilesLeft(id, exception.GetType().Name);
        }
    }
}
