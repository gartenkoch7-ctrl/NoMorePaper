using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NoMorePaper.Application.Trash;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Trash;

/// <summary>
/// The trash in Postgres. An entry belongs to the owner of its document, joined in every read; the
/// reads switch the trash filter of documents off (<see cref="TrashSql.FilterName"/>), because the
/// documents they look at may lie in the trash. A change is opened only when none is open, so this store
/// and <c>EfDocumentFieldsStore</c> share one transaction per scope.
/// </summary>
public sealed class EfTrashStore(AppDbContext db) : ITrashStore
{
    private IDbContextTransaction? _change;

    public async Task AddAsync(TrashItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        db.Add(TrashItemRow.From(item));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TrashEntry>> ListAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        // The filter is off for the whole query (OwnedBy), so both subqueries see documents in the trash.
        var rows = await OwnedBy(ownerId)
            .OrderByDescending(row => row.DeletedAt)
            .ThenByDescending(row => row.Id)
            .Select(row => new
            {
                Row = row,
                Swaps = row.Kind == TrashItemKind.Edited && db.Documents.Any(document =>
                    document.Id == row.DocumentId && document.Files.Any(file => file.Kind == DocumentFileKind.Edited)),
                DocumentInTrash = row.Kind != TrashItemKind.Document && db.Documents.Any(document =>
                    document.Id == row.DocumentId && document.DeletedAt != null),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. rows.Select(found => new TrashEntry(found.Row.ToItem(), found.Swaps, found.DocumentInTrash))];
    }

    public async Task<TrashItem?> FindAsync(Guid id, Guid ownerId, CancellationToken cancellationToken)
    {
        var row = await OwnedBy(ownerId).SingleOrDefaultAsync(found => found.Id == id, cancellationToken).ConfigureAwait(false);
        return row?.ToItem();
    }

    public async Task<TrashItem?> FindLockedAsync(Guid id, Guid? ownerId, CancellationToken cancellationToken)
    {
        var documentId = await db.Set<TrashItemRow>().AsNoTracking()
            .Where(row => row.Id == id)
            .Select(row => (Guid?)row.DocumentId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (documentId is not { } locked)
        {
            return null;
        }

        await LockDocumentAsync(locked, cancellationToken).ConfigureAwait(false);
        var rows = ownerId is { } owner ? OwnedBy(owner) : db.Set<TrashItemRow>().AsNoTracking();
        var row = await rows.SingleOrDefaultAsync(found => found.Id == id, cancellationToken).ConfigureAwait(false);
        return row?.ToItem();
    }

    public async Task<Document?> FindTrashedDocumentAsync(Guid documentId, Guid ownerId, CancellationToken cancellationToken)
    {
        await LockDocumentAsync(documentId, cancellationToken).ConfigureAwait(false);
        return await db.Documents
            .IgnoreQueryFilters([TrashSql.FilterName])
            .SingleOrDefaultAsync(
                document => document.Id == documentId && document.OwnerId == ownerId && document.DeletedAt != null, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RemoveAsync(TrashItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        await db.Set<TrashItemRow>().Where(row => row.Id == item.Id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>?> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        // Only a document in the trash; its owned files go with it, the database cascades the rest.
        var document = await db.Documents
            .IgnoreQueryFilters([TrashSql.FilterName])
            .SingleOrDefaultAsync(found => found.Id == documentId && found.DeletedAt != null, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return null;
        }

        var entries = await db.Set<TrashItemRow>().AsNoTracking()
            .Where(row => row.DocumentId == documentId)
            .Select(row => row.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        db.Documents.Remove(document);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entries;
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (_change is null)
        {
            return;
        }

        await _change.CommitAsync(cancellationToken).ConfigureAwait(false);
        await _change.DisposeAsync().ConfigureAwait(false);
        _change = null;
    }

    public async Task<IReadOnlyList<TrashItem>> DueAsync(DateTimeOffset now, int max, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(max, 1);
        var rows = await db.Set<TrashItemRow>().AsNoTracking()
            .Where(row => row.PurgeAfter < now)
            .OrderBy(row => row.PurgeAfter)
            .ThenBy(row => row.Id)
            .Take(max)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. rows.Select(row => row.ToItem())];
    }

    // Locked before it is read, in the order every change of a document uses (document, then job). Joins
    // an open change, e.g. one EfDocumentFieldsStore opened; otherwise this store opens and commits it.
    private async Task LockDocumentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            _change = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        }

        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM documents WHERE id = {documentId} FOR NO KEY UPDATE", cancellationToken)
            .ConfigureAwait(false);
    }

    // The entries whose document the account owns now, whether the document lies in the trash or not.
    private IQueryable<TrashItemRow> OwnedBy(Guid ownerId) =>
        db.Set<TrashItemRow>().AsNoTracking()
            .Where(row => db.Documents.Any(document => document.Id == row.DocumentId && document.OwnerId == ownerId))
            .IgnoreQueryFilters([TrashSql.FilterName]);
}
