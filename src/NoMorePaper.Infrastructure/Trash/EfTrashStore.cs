using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NoMorePaper.Application.Trash;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Trash;

/// <summary>
/// The trash in Postgres. An entry belongs to the owner of its document, joined in every read; the
/// reads switch the trash filter of documents off (<see cref="TrashSql.FilterName"/>), because the
/// documents they look at may lie in the trash.
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

    public async Task<IReadOnlyList<TrashItem>> ListAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        var rows = await OwnedBy(ownerId)
            .OrderByDescending(row => row.DeletedAt)
            .ThenByDescending(row => row.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. rows.Select(row => row.ToItem())];
    }

    public async Task<TrashItem?> FindAsync(Guid id, Guid ownerId, CancellationToken cancellationToken)
    {
        var row = await OwnedBy(ownerId).SingleOrDefaultAsync(found => found.Id == id, cancellationToken).ConfigureAwait(false);
        return row?.ToItem();
    }

    public async Task<Document?> FindTrashedDocumentAsync(Guid documentId, Guid ownerId, CancellationToken cancellationToken)
    {
        // Locked before it is read, in the order every change of a document uses (document, then job).
        if (db.Database.CurrentTransaction is null)
        {
            _change = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        }

        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM documents WHERE id = {documentId} FOR NO KEY UPDATE", cancellationToken)
            .ConfigureAwait(false);
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

    // The entries whose document the account owns now, whether the document lies in the trash or not.
    private IQueryable<TrashItemRow> OwnedBy(Guid ownerId) =>
        db.Set<TrashItemRow>().AsNoTracking()
            .Where(row => db.Documents.Any(document => document.Id == row.DocumentId && document.OwnerId == ownerId))
            .IgnoreQueryFilters([TrashSql.FilterName]);
}
