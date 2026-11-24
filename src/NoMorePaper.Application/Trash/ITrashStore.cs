using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Trash;

/// <summary>
/// The entries of the trash in the database (<c>trash_items</c>). An entry belongs to the owner of its
/// document; every read filters by it in the query. Writes join the caller's open change (e.g. the one
/// <see cref="Classification.IDocumentFieldsStore.FindVisibleAsync"/>, <see cref="FindLockedAsync"/> or
/// <see cref="FindTrashedDocumentAsync"/> opened; a later lock of another store joins it too);
/// <see cref="SaveAsync"/> commits a change this store opened.
/// </summary>
public interface ITrashStore
{
    /// <summary>Records a new entry, together with the tracked changes of the open change.</summary>
    Task AddAsync(TrashItem item, CancellationToken cancellationToken);

    /// <summary>The entries of documents <paramref name="ownerId"/> owns, newest first, with what a restore would do.</summary>
    Task<IReadOnlyList<TrashEntry>> ListAsync(Guid ownerId, CancellationToken cancellationToken);

    /// <returns>The entry, or <c>null</c> if it does not exist or its document belongs to another account.</returns>
    Task<TrashItem?> FindAsync(Guid id, Guid ownerId, CancellationToken cancellationToken);

    /// <summary>
    /// An entry with its document row locked against other changes (opening a change unless one is open),
    /// so a restore and a removal of the same entry take turns. Read after the lock: an entry removed
    /// meanwhile is not found.
    /// </summary>
    /// <param name="ownerId">Only an entry of a document this account owns; <c>null</c> for any account (the daily purge).</param>
    /// <returns><c>null</c> if it does not exist or its document belongs to another account.</returns>
    Task<TrashItem?> FindLockedAsync(Guid id, Guid? ownerId, CancellationToken cancellationToken);

    /// <summary>
    /// A document in the trash that <paramref name="ownerId"/> owns, locked against other changes and
    /// tracked for <see cref="SaveAsync"/>; the only way to load a document the trash filter hides.
    /// </summary>
    /// <returns><c>null</c> if it does not exist, is not in the trash or belongs to another account.</returns>
    Task<Document?> FindTrashedDocumentAsync(Guid documentId, Guid ownerId, CancellationToken cancellationToken);

    /// <summary>Removes an entry (restored or removed for good), together with the tracked changes of the open change.</summary>
    Task RemoveAsync(TrashItem item, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a document in the trash for good, with everything the database keeps of it: its files, tags,
    /// editors, job, pages, planned edit and every trash entry of it. Its stored files stay; the caller
    /// removes them after <see cref="SaveAsync"/>. The caller has locked the row (<see cref="FindLockedAsync"/>).
    /// </summary>
    /// <returns>
    /// The ids of the document's trash entries, whose trash folders the caller removes too; <c>null</c>,
    /// deleting nothing, if the document does not exist or is not in the trash.
    /// </returns>
    Task<IReadOnlyList<Guid>?> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken);

    /// <summary>Writes the tracked changes and commits a change this store opened.</summary>
    Task SaveAsync(CancellationToken cancellationToken);

    /// <summary>Entries whose time is up (<see cref="TrashItem.PurgeAfter"/> before <paramref name="now"/>), of all accounts, oldest first.</summary>
    /// <param name="max">At most this many.</param>
    Task<IReadOnlyList<TrashItem>> DueAsync(DateTimeOffset now, int max, CancellationToken cancellationToken);
}
