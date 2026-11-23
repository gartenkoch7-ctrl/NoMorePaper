using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Trash;

/// <summary>
/// The entries of the trash in the database (<c>trash_items</c>). An entry belongs to the owner of its
/// document; every read filters by it in the query. Writes join the caller's open change (e.g. the one
/// <see cref="Classification.IDocumentFieldsStore.FindVisibleAsync"/> or <see cref="FindTrashedDocumentAsync"/>
/// opened); <see cref="SaveAsync"/> commits a change this store opened.
/// </summary>
public interface ITrashStore
{
    /// <summary>Records a new entry, together with the tracked changes of the open change.</summary>
    Task AddAsync(TrashItem item, CancellationToken cancellationToken);

    /// <summary>The entries of documents <paramref name="ownerId"/> owns, newest first.</summary>
    Task<IReadOnlyList<TrashItem>> ListAsync(Guid ownerId, CancellationToken cancellationToken);

    /// <returns>The entry, or <c>null</c> if it does not exist or its document belongs to another account.</returns>
    Task<TrashItem?> FindAsync(Guid id, Guid ownerId, CancellationToken cancellationToken);

    /// <summary>
    /// A document in the trash that <paramref name="ownerId"/> owns, locked against other changes and
    /// tracked for <see cref="SaveAsync"/>; the only way to load a document the trash filter hides.
    /// </summary>
    /// <returns><c>null</c> if it does not exist, is not in the trash or belongs to another account.</returns>
    Task<Document?> FindTrashedDocumentAsync(Guid documentId, Guid ownerId, CancellationToken cancellationToken);

    /// <summary>Removes an entry (restored or removed for good), together with the tracked changes of the open change.</summary>
    Task RemoveAsync(TrashItem item, CancellationToken cancellationToken);

    /// <summary>Writes the tracked changes and commits a change this store opened.</summary>
    Task SaveAsync(CancellationToken cancellationToken);

    /// <summary>Entries whose time is up (<see cref="TrashItem.PurgeAfter"/> before <paramref name="now"/>), of all accounts, oldest first.</summary>
    /// <param name="max">At most this many.</param>
    Task<IReadOnlyList<TrashItem>> DueAsync(DateTimeOffset now, int max, CancellationToken cancellationToken);
}
